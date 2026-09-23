using Microsoft.Extensions.Logging;
using Sinchrony.Application.Subscriptions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

// Cobra a renovação automática de um pacote recorrente (Package.IsRecurring) diretamente no
// cartão salvo, no vencimento do ciclo — substitui o modelo antigo de assinatura Asaas
// (DEMANDA_RECORRENCIA_COMPRA_E_CANCELAMENTO_BACKEND_V2.md). Único lugar que sabe aplicar
// sucesso/falha de uma renovação: usado pelo job (cobrança nova), pelo webhook (confirmação/
// recusa de uma cobrança que ficou "pending") e pelo /sync manual.
public class RecurringRenewalService(
    IStudentPackageRepository studentPackageRepository,
    IPurchaseRepository purchaseRepository,
    ICardRepository cardRepository,
    IUserRepository userRepository,
    IAsaasService asaasService,
    ISettingsRepository settingsRepository,
    IEmailService emailService,
    IAuditService auditService,
    SubscriptionOverdueService subscriptionOverdueService,
    StudentPackageLifecycleService studentPackageLifecycleService,
    ILogger<RecurringRenewalService> logger)
{
    public Task<List<Guid>> ListDueAsync(CancellationToken ct) =>
        studentPackageRepository.ListDueForRenewalAsync(DateTime.UtcNow, ct);

    public Task<IEnumerable<Purchase>> ListStalePendingAsync(DateTime olderThan, CancellationToken ct) =>
        purchaseRepository.ListStalePendingRenewalsAsync(olderThan, ct);

    // Tenta cobrar o próximo ciclo de um pacote recorrente.
    public async Task ProcessRenewalAsync(Guid studentPackageId, CancellationToken ct)
    {
        var sp = await studentPackageRepository.GetByIdAsync(studentPackageId, ct);
        if (sp is null || !sp.AutoRenew || sp.Status != StudentPackageStatus.active)
            return;

        // Segunda trava contra cobrança repetida, além da seleção em ListDueForRenewalAsync: se
        // já existe uma retentativa agendada pro futuro, não cobra agora mesmo que alguém chame
        // isso diretamente (ex.: /sync chamando ProcessRenewalAsync fora de hora).
        if (sp.NextRenewalAttemptAt is { } nextAttempt && nextAttempt > DateTime.UtcNow)
            return;

        // Já existe uma renovação em andamento (pending) ou confirmada pra este ciclo — evita
        // cobrar duas vezes (o job pode rodar de novo antes do webhook da tentativa anterior).
        if (await purchaseRepository.HasActiveRenewalForCycleAsync(sp.Id, sp.StartDate, ct))
            return;

        var user = await userRepository.GetByIdAsync(sp.StudentId, ct);
        var package = sp.Package;
        if (user is null || package is null) return;

        var card = sp.RenewalCardId.HasValue
            ? await cardRepository.GetByIdAsync(sp.RenewalCardId.Value, ct)
            : null;
        card ??= (await cardRepository.ListByUserAsync(user.Id, ct)).FirstOrDefault(c => c.IsDefault);

        if (card is null)
        {
            logger.LogWarning("StudentPackage {Id}: no card available for renewal charge.", sp.Id);
            await ApplyRenewalFailureAsync(sp, user, null, "Nenhum cartão cadastrado", ct);
            return;
        }

        var customerId = await asaasService.GetOrCreateCustomerAsync(user.Name, user.Email, user.Cpf, ct);

        string transactionId;
        string status;
        try
        {
            var result = await asaasService.ChargeCardAsync(
                customerId, card.Token, package.Price, $"4Sinchrony - {package.Name} (renovação)", ct);
            transactionId = result.TransactionId;
            status = result.Status;
        }
        catch (DomainException ex)
        {
            // A Asaas recusou de forma síncrona (cartão inválido/expirado etc.) — trata como
            // recusa da tentativa, não como erro do job.
            var failedPurchase = Purchase.CreateRenewalPending(user.Id, package.Id, sp.Id, package.Price, null);
            failedPurchase.Fail();
            await purchaseRepository.AddAsync(failedPurchase, ct);
            await purchaseRepository.SaveAsync(ct);

            await ApplyRenewalFailureAsync(sp, user, failedPurchase, ex.Message, ct);
            return;
        }

        var purchase = Purchase.CreateRenewalPending(user.Id, package.Id, sp.Id, package.Price, transactionId);
        await purchaseRepository.AddAsync(purchase, ct);
        await purchaseRepository.SaveAsync(ct);

        if (status is "CONFIRMED" or "RECEIVED")
        {
            purchase.Confirm();
            await purchaseRepository.SaveAsync(ct);
            await ApplyRenewalSuccessAsync(sp, user, purchase, ct);
        }
        // PENDING (antifraude): fica aguardando o webhook confirmar/recusar, ou a reconciliação
        // do job pegar se ele se perder.
    }

    // Webhook: uma renovação que estava "pending" foi confirmada.
    public async Task ConfirmRenewalAsync(Purchase purchase, CancellationToken ct)
    {
        if (purchase.Status != "pending" || purchase.StudentPackageId is not { } spId) return;

        var sp = await studentPackageRepository.GetByIdAsync(spId, ct);
        var user = sp is null ? null : await userRepository.GetByIdAsync(sp.StudentId, ct);
        if (sp is null || user is null) return;

        purchase.Confirm();
        await purchaseRepository.SaveAsync(ct);
        await ApplyRenewalSuccessAsync(sp, user, purchase, ct);
    }

    // Webhook: uma renovação que estava "pending" foi recusada.
    public async Task RefuseRenewalAsync(Purchase purchase, string? failureReason, CancellationToken ct)
    {
        if (purchase.Status != "pending" || purchase.StudentPackageId is not { } spId) return;

        var sp = await studentPackageRepository.GetByIdAsync(spId, ct);
        var user = sp is null ? null : await userRepository.GetByIdAsync(sp.StudentId, ct);
        if (sp is null || user is null) return;

        purchase.Fail();
        await purchaseRepository.SaveAsync(ct);
        await ApplyRenewalFailureAsync(sp, user, purchase, failureReason, ct);
    }

    // POST /api/subscriptions/{id}/sync: se há uma renovação pendente, confere o status real na
    // Asaas (reconciliação); senão, se o pacote está em retentativa/vencido, tenta cobrar agora.
    public async Task SyncAsync(Guid studentPackageId, CancellationToken ct)
    {
        var sp = await studentPackageRepository.GetByIdAsync(studentPackageId, ct);
        if (sp is null || !sp.AutoRenew) return;

        var pending = await purchaseRepository.GetPendingRenewalAsync(sp.Id, ct);
        if (pending is not null)
        {
            await ReconcilePendingAsync(pending, ct);
            return;
        }

        if (sp.PaymentStatus is SubscriptionPaymentStatus.retrying or SubscriptionPaymentStatus.overdue)
            await ProcessRenewalAsync(sp.Id, ct);
    }

    // Consulta a Asaas pelo id do pagamento de uma renovação pending cujo webhook pode ter se
    // perdido, e aplica o resultado real. Usado pelo /sync e pela varredura periódica do job.
    public async Task ReconcilePendingAsync(Purchase purchase, CancellationToken ct)
    {
        if (purchase.Status != "pending" || string.IsNullOrEmpty(purchase.TransactionId))
            return;

        var result = await asaasService.GetPaymentAsync(purchase.TransactionId, ct);

        if (result.Status is "CONFIRMED" or "RECEIVED")
            await ConfirmRenewalAsync(purchase, ct);
        else if (result.Status is "OVERDUE" or "REFUSED" or "REFUNDED" or "CHARGEBACK_REQUESTED" or "PAYMENT_DELETED")
            await RefuseRenewalAsync(purchase, result.FailureReason, ct);
        // Ainda PENDING de verdade na Asaas — não faz nada, tenta de novo na próxima varredura.
    }

    private async Task ApplyRenewalSuccessAsync(StudentPackage sp, User user, Purchase purchase, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Termos 6.3: créditos do ciclo anterior não acumulam pro seguinte. A cobrança acontece
        // até 24h antes do EndDate, então a troca de créditos/cotas só pode acontecer quando o
        // ciclo pago de fato termina — senão o aluno perderia o resto do ciclo que já pagou.
        bool turnedOverNow;
        if (now >= sp.EndDate)
        {
            // Confirmado depois do vencimento (retentativa, regularização de overdue, antifraude
            // demorado) — a virada acontece agora, não espera o job de expiração.
            await studentPackageLifecycleService.TurnoverAsync(sp, user, now, ct);
            turnedOverNow = true;
        }
        else
        {
            // Caso normal — só registra que o ciclo seguinte já está pago. A virada de créditos/
            // cotas acontece no EndDate de verdade (PackageExpirationService, a cada minuto).
            sp.MarkRenewalPaidForCycle();
            turnedOverNow = false;
        }

        sp.MarkUpToDate(now, purchase.Amount);

        var wasBlockedForPayment = user.Status == StudentStatus.blocked && user.BlockedReason == "payment_failed";
        if (wasBlockedForPayment)
            user.Reactivate();

        await userRepository.SaveAsync(ct);
        await studentPackageRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "subscription.renewal_confirmed", "StudentPackage", sp.Id, user.Id,
            $"PurchaseId: {purchase.Id}, TurnedOverNow: {turnedOverNow}, Reactivated: {wasBlockedForPayment}",
            ct: ct);

        logger.LogInformation(
            "StudentPackage {Id}: renewal confirmed for user {UserId} (turned over now: {TurnedOverNow}).",
            sp.Id, user.Id, turnedOverNow);

        if (wasBlockedForPayment && !string.IsNullOrWhiteSpace(user.Email))
        {
            var settings = await settingsRepository.GetAsync(ct);
            var body = $"""
                <h2>Acesso liberado</h2>
                <p>Olá, {user.Name}!</p>
                <p>Seu pagamento foi confirmado e seu acesso ao 4Sinchrony foi reativado. Já pode agendar suas aulas normalmente.</p>
                <br>
                <small>4Sinchrony Experience</small>
                """;
            SendEmailFireAndForget(user.Email, "Acesso liberado — pagamento confirmado", body, settings);
        }
    }

    private async Task ApplyRenewalFailureAsync(
        StudentPackage sp, User user, Purchase? purchase, string? failureReason, CancellationToken ct)
    {
        // Já estava overdue (bloqueado, admins já alertados) — essa tentativa só existe porque o
        // aluno trocou o cartão. Falhando de novo, mantém overdue sem reiniciar as 3 tentativas
        // nem gerar um segundo alerta; só uma cobrança confirmada tira daqui.
        if (sp.PaymentStatus == SubscriptionPaymentStatus.overdue)
        {
            sp.KeepOverdueAfterRetryFailure(failureReason);
            await studentPackageRepository.SaveAsync(ct);

            await auditService.LogAsync(
                "subscription.renewal_retry_failed", "StudentPackage", sp.Id, user.Id,
                $"Reason: {failureReason}", ct: ct);

            logger.LogInformation(
                "StudentPackage {Id}: retry after card update failed, staying overdue.", sp.Id);
            return;
        }

        sp.RecordRenewalFailure(failureReason);
        await studentPackageRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "subscription.renewal_failed", "StudentPackage", sp.Id, user.Id,
            $"Attempt: {sp.RenewalAttempts}, Reason: {failureReason}", ct: ct);

        logger.LogInformation(
            "StudentPackage {Id}: renewal attempt {Attempt} failed ({Reason}).",
            sp.Id, sp.RenewalAttempts, failureReason);

        if (sp.RenewalAttempts >= 3)
        {
            // Sem cobrança real (ex.: sem cartão), não há um id de pagamento — usa uma chave
            // determinística por pacote+ciclo pra o alerta continuar idempotente.
            var transactionId = purchase?.TransactionId
                ?? $"renewal-overdue_{sp.Id}_{sp.EndDate:yyyyMMdd}";
            await subscriptionOverdueService.BlockAndAlertAsync(
                user, sp, transactionId, sp.Package?.Price ?? 0m, DateOnly.FromDateTime(sp.EndDate), ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var settings = await settingsRepository.GetAsync(ct);
            var body = $"""
                <h2>Não conseguimos processar seu pagamento</h2>
                <p>Olá, {user.Name}!</p>
                <p>A cobrança da renovação do seu plano não foi aprovada dessa vez. Vamos tentar novamente automaticamente nos próximos dias.</p>
                <p>Para evitar a suspensão do acesso, atualize o cartão cadastrado no aplicativo.</p>
                <br>
                <small>4Sinchrony Experience</small>
                """;
            SendEmailFireAndForget(user.Email, "Pagamento não aprovado — vamos tentar novamente", body, settings);
        }
    }

    private void SendEmailFireAndForget(string to, string subject, string body, Settings? settings)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendWithSettingsAsync(to, subject, body, settings, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recurring renewal: falha ao enviar e-mail para {Email}.", to);
            }
        });
    }
}
