using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class PaymentConfirmationService(
    IPurchaseRepository purchaseRepository,
    IUserRepository userRepository,
    ICreditTransactionRepository creditTransactionRepository,
    IStudentPackageRepository studentPackageRepository,
    IAuditService auditSvc,
    IAsaasService asaasService,
    RecurringRenewalService recurringRenewalService,
    ILogger<PaymentConfirmationService> logger) : IPaymentConfirmationService
{
    // Compra avulsa OU renovação automática (Purchase.Kind == "renewal"): confirma a Purchase
    // pendente que corresponde ao transactionId. Uma renovação delega pro RecurringRenewalService
    // (credita/encadeia o próximo ciclo do pacote já ativo); o resto segue o fluxo de sempre:
    // credita e ativa o StudentPackage na fila (contratação nova).
    public async Task ConfirmPendingAsync(string transactionId, string eventType, CancellationToken ct = default)
    {
        var purchases = await purchaseRepository.ListByTransactionIdAsync(transactionId, ct);

        var alreadyConfirmed = purchases.Any(p => p.Status == "confirmed");

        if (alreadyConfirmed)
        {
            logger.LogInformation("Asaas webhook: transaction {Id} already processed.", transactionId);
            return;
        }

        var pendingPurchases = purchases.Where(p => p.Status == "pending").ToList();

        if (pendingPurchases.Count == 0)
        {
            logger.LogWarning("Asaas webhook: no pending purchase found for {Id}.", transactionId);
            return;
        }

        foreach (var purchase in pendingPurchases)
        {
            if (purchase.Kind == "renewal")
            {
                await recurringRenewalService.ConfirmRenewalAsync(purchase, ct);
                continue;
            }

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
                    // Legado: se o pacote substituído vinha de uma assinatura Asaas, encerra a
                    // cobrança por lá — o modelo atual (AutoRenew) não precisa disso, Cancel()
                    // já desliga a renovação localmente.
                    if (activePackage?.AsaasSubscriptionId is not null)
                        await asaasService.CancelSubscriptionAsync(activePackage.AsaasSubscriptionId, ct);

                    activePackage?.Cancel();
                    queuedPackage.Activate();
                    logger.LogInformation(
                        "StudentPackage {Id} activated for user {UserId}.",
                        queuedPackage.Id, purchase.UserId);
                }
                else if (activePackage.AutoRenew)
                {
                    // Pacote na fila atrás de um ativo recorrente: o ativo renova sozinho ~24h antes de
                    // vencer e nunca chegaria a expirar — sem desligar a renovação aqui, o aluno pagaria
                    // pelo novo pacote e continuaria sendo cobrado pelo antigo pra sempre. Termina no
                    // EndDate normalmente, como qualquer cancelamento de renovação.
                    activePackage.CancelRenewal();
                    logger.LogInformation(
                        "StudentPackage {ActiveId} teve a renovação cancelada — pacote {QueuedId} " +
                        "na fila vai assumir quando o ativo vencer (user {UserId}).",
                        activePackage.Id, queuedPackage.Id, purchase.UserId);
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
}
