using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Payments;

// De onde vem a tentativa de compra — define o que fazer com uma cobrança em aberto do mesmo pacote.
public enum PurchaseOrigin
{
    AppPix,   // aluno gerando PIX: reaproveita o PIX pendente dele, se houver
    AppCard,  // aluno pagando no cartão: não dá pra devolver um PIX, então recusa
    Counter   // recepção lançando no balcão (AssignPackage): recusa
}

// Regra de compra única (Package.OncePerStudent, ex.: "Primeira Experiência"):
//  - só conta compra PAGA (confirmed), do aluno OU da família dele (ele, o responsável, os
//    dependentes) — a concessão no balcão também conta, e nem o admin libera uma segunda;
//  - PIX gerado e não pago não conta como "já comprou", mas também não pode haver mais de uma
//    cobrança em aberto do pacote pra mesma família (senão a pessoa gera dois PIX e paga os dois).
// Checagem + criação da Purchase precisam rodar dentro de RunLockedAsync, que serializa as
// requisições concorrentes da mesma família+pacote com um advisory lock do Postgres.
public class OncePerStudentGuard(
    IUserRepository userRepository,
    IPurchaseRepository purchaseRepository,
    IAsaasService asaasService,
    IPaymentConfirmationService paymentConfirmationService,
    IUnitOfWork unitOfWork)
{
    public const string AlreadyPurchasedCode = "PACKAGE_ALREADY_PURCHASED";
    public const string PaymentInProgressCode = "PAYMENT_ALREADY_IN_PROGRESS";
    public const string NotForDependentCode = "PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT";

    // A cobrança PIX é criada com dueDate = hoje(UTC) + 1 dia (AsaasService.CreatePixChargeAsync) e
    // fica pagável até o fim desse dia. Só consideramos vencida a partir do começo do dia seguinte
    // ao vencimento (+1 dia de folga pro fuso do Asaas), nunca antes — cancelar um PIX ainda
    // pagável faria o aluno pagar uma cobrança que não vale mais.
    public static bool IsPixExpired(Purchase purchase, DateTime utcNow)
        => utcNow >= purchase.CreatedAt.Date.AddDays(2);

    // Executa `action` sob trava por família+pacote quando o carrinho tem algum pacote de compra
    // única; senão só executa. Os erros de BLOQUEIO desta própria guarda ainda fazem commit: o que a
    // checagem já gravou (ex.: PIX vencido marcado como failed, cobrança paga confirmada) é legítimo
    // e não pode ser desfeito junto com a recusa. Qualquer outro erro desfaz tudo.
    public async Task<T> RunLockedAsync<T>(
        Guid userId, IReadOnlyCollection<Package> packages,
        Func<Task<T>> action, CancellationToken ct)
    {
        var restricted = packages.Where(p => p.OncePerStudent).ToList();
        if (restricted.Count == 0)
            return await action();

        var family = await userRepository.GetFamilyAsync(userId, ct);

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Ordem estável das chaves evita deadlock entre dois carrinhos com os mesmos pacotes.
            foreach (var pkg in restricted.OrderBy(p => p.Id))
                await unitOfWork.AcquireAdvisoryLockAsync($"once-per-student:{family.RootId}:{pkg.Id}", ct);

            var result = await action();
            await unitOfWork.CommitAsync(ct);
            return result;
        }
        catch (DomainException ex) when (ex.Code is AlreadyPurchasedCode or PaymentInProgressCode)
        {
            await unitOfWork.CommitAsync(ct);
            throw;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    // Lança se a compra não pode seguir. Devolve um PIX pendente (ainda válido, do próprio
    // usuário) quando o chamador deve devolver o mesmo PIX em vez de gerar outro; null = segue.
    public async Task<Purchase?> CheckAsync(
        Guid userId, IReadOnlyCollection<Package> packages, PurchaseOrigin origin, CancellationToken ct)
    {
        var restricted = packages.Where(p => p.OncePerStudent).ToList();
        if (restricted.Count == 0) return null;

        var family = await userRepository.GetFamilyAsync(userId, ct);
        Purchase? reusable = null;

        foreach (var pkg in restricted)
        {
            var found = await CheckPackageAsync(userId, pkg, family, origin, ct);
            reusable ??= found;
        }

        if (reusable is not null && packages.Count > 1)
            throw PaymentInProgress();

        return reusable;
    }

    private async Task<Purchase?> CheckPackageAsync(
        Guid userId, Package package, StudentFamily family, PurchaseOrigin origin, CancellationToken ct)
    {
        // Quem já é dependente não compra pacote de compra única (ex.: Primeira Experiência é para
        // quem ainda não é aluno do estúdio). RootId != userId vale nos dois modelos de dependente.
        if (family.RootId != userId)
            throw DomainException.Conflict(NotForDependentCode,
                "Este pacote não está disponível para dependentes.");

        var purchases = await purchaseRepository.ListConfirmedOrPendingByPackageAndUsersAsync(
            package.Id, family.UserIds, ct);

        if (purchases.Any(p => p.Status == "confirmed"))
            throw AlreadyPurchased();

        var now = DateTime.UtcNow;
        Purchase? reusable = null;

        // Um carrinho gera N purchases com o mesmo transactionId — trata cada cobrança uma vez só.
        foreach (var charge in purchases.Where(p => p.Status == "pending").GroupBy(p => p.TransactionId))
        {
            var first = charge.First();

            // Cartão pendente = em análise antifraude. Não dá pra reaproveitar nem cancelar.
            if (first.PaymentMethod == "card")
                throw PaymentInProgress("Já existe um pagamento deste pacote em análise. Aguarde a confirmação.");

            if (string.IsNullOrEmpty(first.TransactionId))
            {
                // PIX pendente sem cobrança associada não pode ser pago nunca — descarta.
                foreach (var orphan in charge) orphan.Fail();
                await purchaseRepository.SaveAsync(ct);
                continue;
            }

            if (!IsPixExpired(first, now))
            {
                if (origin == PurchaseOrigin.Counter)
                    throw PaymentInProgress("Existe um pagamento pendente deste pacote pelo app.");
                if (origin == PurchaseOrigin.AppCard || first.UserId != userId)
                    throw PaymentInProgress("Já existe um PIX pendente deste pacote. Conclua o pagamento ou aguarde o vencimento.");

                reusable ??= first;
                continue;
            }

            // PIX vencido: confere no Asaas antes de descartar.
            var status = await asaasService.GetPaymentAsync(first.TransactionId, ct);
            if (status.Status is "CONFIRMED" or "RECEIVED" or "RECEIVED_IN_CASH")
            {
                // Foi pago e o webhook se perdeu: confirma pelo fluxo normal — a família já comprou.
                await paymentConfirmationService.ConfirmPendingAsync(first.TransactionId, "PAYMENT_RECEIVED", ct);
                throw AlreadyPurchased();
            }

            await asaasService.CancelPaymentAsync(first.TransactionId, ct);
            var sameCharge = await purchaseRepository.ListByTransactionIdAsync(first.TransactionId, ct);
            foreach (var p in sameCharge.Where(p => p.Status == "pending")) p.Fail();
            await purchaseRepository.SaveAsync(ct);
        }

        return reusable;
    }

    private static DomainException AlreadyPurchased()
        => DomainException.Conflict(AlreadyPurchasedCode,
            "Este pacote pode ser adquirido apenas uma vez por aluno.");

    private static DomainException PaymentInProgress(string? message = null)
        => DomainException.Conflict(PaymentInProgressCode,
            message ?? "Já existe um pagamento deste pacote em andamento. Conclua ou aguarde a confirmação.");
}
