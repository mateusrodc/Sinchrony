using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Text.Json;

namespace Sinchrony.Api.Controllers.App;

[ApiController]
[Route("webhooks")]
[Produces("application/json")]
public class WebhooksController(
    IServiceScopeFactory scopeFactory,
    IAuditService auditService,
    ILogger<WebhooksController> logger,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("asaas")]
    public async Task<IActionResult> Asaas([FromBody] JsonElement payload, CancellationToken ct)
    {
        var webhookToken = configuration["Asaas:WebhookToken"];
        if (string.IsNullOrEmpty(webhookToken))
        {
            logger.LogWarning("Asaas webhook received but WebhookToken is not configured.");
            return Unauthorized();
        }

        Request.Headers.TryGetValue("asaas-access-token", out var receivedToken);
        if (receivedToken.ToString() != webhookToken)
        {
            logger.LogWarning("Asaas webhook received with invalid token.");
            return Unauthorized();
        }

        var payloadCopy = payload.Clone();

        // Cria novo escopo de DI para o background task — evita DbContext disposed
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var purchaseRepository = scope.ServiceProvider.GetRequiredService<IPurchaseRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var creditTransactionRepository = scope.ServiceProvider.GetRequiredService<ICreditTransactionRepository>();
            var studentPackageRepository = scope.ServiceProvider.GetRequiredService<IStudentPackageRepository>();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var auditSvc = scope.ServiceProvider.GetRequiredService<IAuditService>();

            try
            {
                await ProcessWebhookAsync(payloadCopy, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository, settingsRepository,
                    emailService, auditSvc, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Asaas webhook in background.");
            }
        });

        return Ok(new { message = "Received." });
    }

    private async Task ProcessWebhookAsync(
        JsonElement payload,
        IPurchaseRepository purchaseRepository,
        IUserRepository userRepository,
        ICreditTransactionRepository creditTransactionRepository,
        IStudentPackageRepository studentPackageRepository,
        ISettingsRepository settingsRepository,
        IEmailService emailService,
        IAuditService auditSvc,
        CancellationToken ct)
    {
        var eventType = payload.TryGetProperty("event", out var ev)
            ? ev.GetString() : null;

        logger.LogInformation("Asaas webhook event: {Event}", eventType);

        if (!payload.TryGetProperty("payment", out var paymentEl)) return;

        var transactionId = paymentEl.TryGetProperty("id", out var idEl)
            ? idEl.GetString() : null;

        if (string.IsNullOrEmpty(transactionId)) return;

        var subscriptionId = paymentEl.TryGetProperty("subscription", out var subEl)
            ? subEl.GetString() : null;

        var paymentValue = paymentEl.TryGetProperty("value", out var valueEl) && valueEl.TryGetDecimal(out var v)
            ? v : 0m;

        logger.LogInformation("Asaas webhook processing transactionId: {Id}, subscription: {SubscriptionId}",
            transactionId, subscriptionId);

        switch (eventType)
        {
            case "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED":
                if (!string.IsNullOrEmpty(subscriptionId))
                    await HandleSubscriptionPaymentConfirmedAsync(
                        subscriptionId, transactionId, paymentValue, eventType!,
                        studentPackageRepository, purchaseRepository, userRepository,
                        creditTransactionRepository, settingsRepository, emailService, auditSvc, ct);
                else
                    await HandleAdHocPaymentConfirmedAsync(
                        transactionId, eventType!, purchaseRepository, userRepository,
                        creditTransactionRepository, studentPackageRepository, auditSvc, ct);
                break;

            case "PAYMENT_OVERDUE":
                if (!string.IsNullOrEmpty(subscriptionId))
                    await HandleSubscriptionOverdueAsync(
                        subscriptionId, transactionId, paymentValue,
                        studentPackageRepository, purchaseRepository, userRepository,
                        settingsRepository, emailService, auditSvc, ct);
                break;

            case "PAYMENT_REPROVED_BY_RISK_ANALYSIS" or "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED":
                if (!string.IsNullOrEmpty(subscriptionId))
                    await SendPreventiveFailureEmailAsync(
                        subscriptionId, studentPackageRepository, userRepository,
                        settingsRepository, emailService, ct);
                else
                    await HandleAdHocPaymentRefusedAsync(
                        transactionId, eventType!, purchaseRepository, userRepository,
                        creditTransactionRepository, studentPackageRepository, auditSvc, ct);
                break;
        }
    }

    // Compra avulsa (PIX ou cartão único, não vinculada a assinatura): confirma a Purchase
    // pendente que corresponde ao transactionId, credita e ativa o StudentPackage na fila.
    private async Task HandleAdHocPaymentConfirmedAsync(
        string transactionId, string eventType,
        IPurchaseRepository purchaseRepository, IUserRepository userRepository,
        ICreditTransactionRepository creditTransactionRepository,
        IStudentPackageRepository studentPackageRepository,
        IAuditService auditSvc, CancellationToken ct)
    {
        var allPurchases = await purchaseRepository.ListAllAsync(ct);

        var alreadyConfirmed = allPurchases.Any(p =>
            p.TransactionId == transactionId && p.Status == "confirmed");

        if (alreadyConfirmed)
        {
            logger.LogInformation("Asaas webhook: transaction {Id} already processed.", transactionId);
            return;
        }

        var pendingPurchases = allPurchases
            .Where(p => p.TransactionId == transactionId && p.Status == "pending")
            .ToList();

        if (pendingPurchases.Count == 0)
        {
            logger.LogWarning("Asaas webhook: no pending purchase found for {Id}.", transactionId);
            return;
        }

        foreach (var purchase in pendingPurchases)
        {
            purchase.Confirm();

            var user = await userRepository.GetByIdAsync(purchase.UserId, ct);
            if (user is null) continue;

            var credits = purchase.Package?.Credits ?? 0;
            logger.LogInformation("Asaas webhook: adding {Credits} credits to user {UserId}.",
                credits, user.Id);

            if (credits > 0)
            {
                user.AddCredits(credits);
                var creditTx = CreditTransaction.Create(
                    user.Id, credits, user.Credits,
                    $"Card/PIX purchase confirmed: {transactionId}",
                    "purchase", purchase.Id);
                await creditTransactionRepository.AddAsync(creditTx, ct);
            }

            // Ativa StudentPackage queued
            var queuedPackage = await studentPackageRepository
                .GetQueuedByStudentAsync(purchase.UserId, ct);

            if (queuedPackage is not null && queuedPackage.PackageId == purchase.PackageId)
            {
                var activePackage = await studentPackageRepository
                    .GetActiveByStudentAsync(purchase.UserId, ct);

                // Aula Avulsa ativa não segura um plano real pago: substitui na hora.
                if (activePackage is null
                    || queuedPackage.Package?.ReplacesActive(activePackage.Package) == true)
                {
                    activePackage?.Cancel();
                    queuedPackage.Activate();
                    logger.LogInformation(
                        "StudentPackage {Id} activated for user {UserId}.",
                        queuedPackage.Id, purchase.UserId);
                }
            }

            await auditSvc.LogAsync(
                "payment.confirmed", "Purchase",
                purchase.Id, purchase.UserId,
                $"TransactionId: {transactionId}, Event: {eventType}, Credits: {credits}",
                ct: ct);
        }

        await purchaseRepository.SaveAsync(ct);
        await userRepository.SaveAsync(ct);
        await creditTransactionRepository.SaveAsync(ct);
        await studentPackageRepository.SaveAsync(ct);

        logger.LogInformation("Asaas webhook: transaction {Id} processed successfully.", transactionId);
    }

    // Ciclo de assinatura recorrente confirmado (primeira cobrança ou renovação mensal):
    // gera/confirma a Purchase do ciclo, credita, renova a vigência e desbloqueia o aluno se
    // ele estava bloqueado por falha de pagamento dessa mesma assinatura.
    private async Task HandleSubscriptionPaymentConfirmedAsync(
        string subscriptionId, string transactionId, decimal paymentValue, string eventType,
        IStudentPackageRepository studentPackageRepository, IPurchaseRepository purchaseRepository,
        IUserRepository userRepository, ICreditTransactionRepository creditTransactionRepository,
        ISettingsRepository settingsRepository, IEmailService emailService,
        IAuditService auditSvc, CancellationToken ct)
    {
        var studentPackage = await studentPackageRepository.GetByAsaasSubscriptionIdAsync(subscriptionId, ct);
        if (studentPackage is null)
        {
            logger.LogWarning("Asaas webhook: no StudentPackage found for subscription {SubscriptionId}.", subscriptionId);
            return;
        }

        var user = await userRepository.GetByIdAsync(studentPackage.StudentId, ct);
        if (user is null) return;

        var allPurchases = await purchaseRepository.ListAllAsync(ct);
        if (allPurchases.Any(p => p.TransactionId == transactionId && p.Status == "confirmed"))
        {
            logger.LogInformation(
                "Asaas webhook: subscription {SubscriptionId} payment {PaymentId} already processed.",
                subscriptionId, transactionId);
            return;
        }

        var purchase = Purchase.CreateConfirmed(
            user.Id, studentPackage.PackageId, paymentValue, "card", transactionId);
        await purchaseRepository.AddAsync(purchase, ct);

        var credits = studentPackage.Package?.GetCreditsToGrant() ?? 0;
        if (credits > 0)
        {
            user.AddCredits(credits);
            var creditTx = CreditTransaction.Create(
                user.Id, credits, user.Credits,
                $"Recurring subscription payment confirmed: {transactionId}",
                "purchase", purchase.Id);
            await creditTransactionRepository.AddAsync(creditTx, ct);
        }

        if (studentPackage.Status == StudentPackageStatus.queued)
            studentPackage.Activate();
        else
            studentPackage.RenewCycle();

        var wasBlockedForPayment = user.Status == StudentStatus.blocked && user.BlockedReason == "payment_failed";
        if (wasBlockedForPayment)
            user.Reactivate();

        await purchaseRepository.SaveAsync(ct);
        await userRepository.SaveAsync(ct);
        await creditTransactionRepository.SaveAsync(ct);
        await studentPackageRepository.SaveAsync(ct);

        await auditSvc.LogAsync(
            "subscription.payment_confirmed", "StudentPackage",
            studentPackage.Id, user.Id,
            $"SubscriptionId: {subscriptionId}, PaymentId: {transactionId}, Event: {eventType}, Credits: {credits}, Reactivated: {wasBlockedForPayment}",
            ct: ct);

        logger.LogInformation(
            "Asaas webhook: subscription {SubscriptionId} cycle confirmed for user {UserId}.",
            subscriptionId, user.Id);

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

            SendEmailFireAndForget(user.Email, "Acesso liberado — pagamento confirmado", body, settings, emailService);
        }
    }

    // A Asaas desistiu de reprocessar (retentativa automática esgotada) e marcou a cobrança
    // como vencida: só aqui o acesso é bloqueado. Créditos já concedidos ficam intactos.
    private async Task HandleSubscriptionOverdueAsync(
        string subscriptionId, string transactionId, decimal paymentValue,
        IStudentPackageRepository studentPackageRepository, IPurchaseRepository purchaseRepository,
        IUserRepository userRepository, ISettingsRepository settingsRepository,
        IEmailService emailService, IAuditService auditSvc, CancellationToken ct)
    {
        var studentPackage = await studentPackageRepository.GetByAsaasSubscriptionIdAsync(subscriptionId, ct);
        if (studentPackage is null)
        {
            logger.LogWarning("Asaas webhook: no StudentPackage found for overdue subscription {SubscriptionId}.", subscriptionId);
            return;
        }

        var user = await userRepository.GetByIdAsync(studentPackage.StudentId, ct);
        if (user is null) return;

        var allPurchases = await purchaseRepository.ListAllAsync(ct);
        if (!allPurchases.Any(p => p.TransactionId == transactionId))
        {
            var failedPurchase = Purchase.CreatePending(
                user.Id, studentPackage.PackageId, paymentValue, "card", transactionId);
            failedPurchase.Fail();
            await purchaseRepository.AddAsync(failedPurchase, ct);
            await purchaseRepository.SaveAsync(ct);
        }

        if (user.Status == StudentStatus.blocked)
        {
            logger.LogInformation("Asaas webhook: user {UserId} already blocked, skipping.", user.Id);
            return;
        }

        user.Block("payment_failed");
        await userRepository.SaveAsync(ct);

        await auditSvc.LogAsync(
            "subscription.blocked", "User",
            user.Id, user.Id,
            $"SubscriptionId: {subscriptionId}, PaymentId: {transactionId}",
            ct: ct);

        logger.LogInformation(
            "Asaas webhook: user {UserId} blocked after subscription {SubscriptionId} went overdue.",
            user.Id, subscriptionId);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var settings = await settingsRepository.GetAsync(ct);
            var body = $"""
                <h2>Acesso suspenso</h2>
                <p>Olá, {user.Name}!</p>
                <p>Não conseguimos confirmar o pagamento da sua assinatura após várias tentativas e seu acesso ao 4Sinchrony foi suspenso.</p>
                <p>Seus créditos continuam guardados — atualize a forma de pagamento no aplicativo para reativar o acesso automaticamente.</p>
                <br>
                <small>4Sinchrony Experience</small>
                """;

            SendEmailFireAndForget(user.Email, "Acesso suspenso — pagamento não confirmado", body, settings, emailService);
        }
    }

    // Aviso preventivo: a cobrança do ciclo falhou uma vez, mas a Asaas ainda vai reprocessar
    // sozinha por alguns dias antes de declarar OVERDUE. Não bloqueia nada — só avisa.
    private async Task SendPreventiveFailureEmailAsync(
        string subscriptionId, IStudentPackageRepository studentPackageRepository,
        IUserRepository userRepository, ISettingsRepository settingsRepository,
        IEmailService emailService, CancellationToken ct)
    {
        var studentPackage = await studentPackageRepository.GetByAsaasSubscriptionIdAsync(subscriptionId, ct);
        if (studentPackage is null) return;

        var user = await userRepository.GetByIdAsync(studentPackage.StudentId, ct);
        if (user is null || user.Status == StudentStatus.blocked || string.IsNullOrWhiteSpace(user.Email))
            return;

        var settings = await settingsRepository.GetAsync(ct);
        var body = $"""
            <h2>Não conseguimos processar seu pagamento</h2>
            <p>Olá, {user.Name}!</p>
            <p>A cobrança da sua assinatura não foi aprovada dessa vez. Vamos tentar novamente automaticamente nos próximos dias.</p>
            <p>Para evitar a suspensão do acesso, atualize o cartão cadastrado no aplicativo.</p>
            <br>
            <small>4Sinchrony Experience</small>
            """;

        SendEmailFireAndForget(user.Email, "Pagamento não aprovado — vamos tentar novamente", body, settings, emailService);
    }

    // Cobrança avulsa (não vinculada a assinatura) que tinha sido aprovada e depois foi
    // reprovada na análise antifraude ou teve a captura recusada: reverte créditos/pacote já
    // concedidos, se houver.
    private async Task HandleAdHocPaymentRefusedAsync(
        string transactionId, string eventType,
        IPurchaseRepository purchaseRepository, IUserRepository userRepository,
        ICreditTransactionRepository creditTransactionRepository,
        IStudentPackageRepository studentPackageRepository,
        IAuditService auditSvc, CancellationToken ct)
    {
        var allPurchases = await purchaseRepository.ListAllAsync(ct);
        var matches = allPurchases.Where(p => p.TransactionId == transactionId).ToList();

        if (matches.Count == 0)
        {
            logger.LogWarning("Asaas webhook: no purchase found for refused payment {Id}.", transactionId);
            return;
        }

        foreach (var purchase in matches)
        {
            if (purchase.Status == "confirmed")
            {
                var user = await userRepository.GetByIdAsync(purchase.UserId, ct);
                if (user is not null)
                {
                    var credits = purchase.Package?.Credits ?? 0;
                    var toDeduct = Math.Min(credits, user.Credits);
                    if (toDeduct > 0)
                    {
                        user.DeductCredits(toDeduct);
                        var creditTx = CreditTransaction.Create(
                            user.Id, -toDeduct, user.Credits,
                            $"Card payment reversed ({eventType}): {transactionId}",
                            "refund", purchase.Id);
                        await creditTransactionRepository.AddAsync(creditTx, ct);
                        await creditTransactionRepository.SaveAsync(ct);
                    }

                    var active = await studentPackageRepository.GetActiveByStudentAsync(purchase.UserId, ct);
                    if (active is not null && active.PackageId == purchase.PackageId)
                        active.Cancel();

                    await userRepository.SaveAsync(ct);
                    await studentPackageRepository.SaveAsync(ct);
                }
            }

            purchase.Fail();

            await auditSvc.LogAsync(
                "payment.reproved", "Purchase",
                purchase.Id, purchase.UserId,
                $"TransactionId: {transactionId}, Event: {eventType}",
                ct: ct);
        }

        await purchaseRepository.SaveAsync(ct);
    }

    private void SendEmailFireAndForget(
        string to, string subject, string body, Settings? settings, IEmailService emailService)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendWithSettingsAsync(to, subject, body, settings, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Asaas webhook: falha ao enviar e-mail para {Email}.", to);
            }
        });
    }
}
