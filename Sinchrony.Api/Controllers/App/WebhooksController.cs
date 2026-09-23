using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Services;
using System.Text.Json;

namespace Sinchrony.Api.Controllers.App;

[ApiController]
[Route("webhooks")]
[Produces("application/json")]
public class WebhooksController(
    IServiceScopeFactory scopeFactory,
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
            var auditSvc = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var asaasService = scope.ServiceProvider.GetRequiredService<IAsaasService>();
            var recurringRenewalService = scope.ServiceProvider.GetRequiredService<RecurringRenewalService>();

            try
            {
                await ProcessWebhookAsync(payloadCopy, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository,
                    auditSvc, asaasService, recurringRenewalService, CancellationToken.None);
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
        IAuditService auditSvc,
        IAsaasService asaasService,
        RecurringRenewalService recurringRenewalService,
        CancellationToken ct)
    {
        var eventType = payload.TryGetProperty("event", out var ev)
            ? ev.GetString() : null;

        logger.LogInformation("Asaas webhook event: {Event}", eventType);

        if (!payload.TryGetProperty("payment", out var paymentEl)) return;

        var transactionId = paymentEl.TryGetProperty("id", out var idEl)
            ? idEl.GetString() : null;

        if (string.IsNullOrEmpty(transactionId)) return;

        var paymentValue = paymentEl.TryGetProperty("value", out var valueEl) && valueEl.TryGetDecimal(out var v)
            ? v : 0m;

        // A Asaas não expõe um campo dedicado de "motivo da recusa" nesses eventos — usa a
        // description do pagamento como melhor aproximação disponível.
        var failureReason = paymentEl.TryGetProperty("description", out var descEl)
            ? descEl.GetString() : null;

        logger.LogInformation("Asaas webhook processing transactionId: {Id}", transactionId);

        switch (eventType)
        {
            case "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED":
                await HandleAdHocPaymentConfirmedAsync(
                    transactionId, eventType!, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository, auditSvc, asaasService,
                    recurringRenewalService, ct);
                break;

            case "PAYMENT_REPROVED_BY_RISK_ANALYSIS" or "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED":
                await HandleAdHocPaymentRefusedAsync(
                    transactionId, eventType!, failureReason, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository, auditSvc,
                    recurringRenewalService, ct);
                break;
        }
    }

    // Compra avulsa OU renovação automática (Purchase.Kind == "renewal"): confirma a Purchase
    // pendente que corresponde ao transactionId. Uma renovação delega pro RecurringRenewalService
    // (credita/encadeia o próximo ciclo do pacote já ativo); o resto segue o fluxo de sempre:
    // credita e ativa o StudentPackage na fila (contratação nova).
    private async Task HandleAdHocPaymentConfirmedAsync(
        string transactionId, string eventType,
        IPurchaseRepository purchaseRepository, IUserRepository userRepository,
        ICreditTransactionRepository creditTransactionRepository,
        IStudentPackageRepository studentPackageRepository,
        IAuditService auditSvc, IAsaasService asaasService,
        RecurringRenewalService recurringRenewalService, CancellationToken ct)
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

    // Cobrança avulsa/renovação que foi reprovada na análise antifraude ou teve a captura
    // recusada. Uma renovação (Kind=renewal) delega pro RecurringRenewalService (conta como
    // tentativa falha, agenda retentativa ou bloqueia se já eram 3); o resto reverte
    // créditos/pacote já concedidos, se a purchase avulsa já tinha sido confirmada antes.
    private async Task HandleAdHocPaymentRefusedAsync(
        string transactionId, string eventType, string? failureReason,
        IPurchaseRepository purchaseRepository, IUserRepository userRepository,
        ICreditTransactionRepository creditTransactionRepository,
        IStudentPackageRepository studentPackageRepository,
        IAuditService auditSvc, RecurringRenewalService recurringRenewalService, CancellationToken ct)
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
            if (purchase.Kind == "renewal")
            {
                await recurringRenewalService.RefuseRenewalAsync(purchase, failureReason, ct);
                continue;
            }

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
}
