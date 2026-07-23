using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sinchrony.Domain.Entities;
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
            var auditSvc = scope.ServiceProvider.GetRequiredService<IAuditService>();

            try
            {
                await ProcessWebhookAsync(payloadCopy, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository, auditSvc,
                    CancellationToken.None);
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
        CancellationToken ct)
    {
        var eventType = payload.TryGetProperty("event", out var ev)
            ? ev.GetString() : null;

        logger.LogInformation("Asaas webhook event: {Event}", eventType);

        if (eventType is not ("PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED"))
            return;

        if (!payload.TryGetProperty("payment", out var paymentEl)) return;

        var transactionId = paymentEl.TryGetProperty("id", out var idEl)
            ? idEl.GetString() : null;

        if (string.IsNullOrEmpty(transactionId)) return;

        logger.LogInformation("Asaas webhook processing transactionId: {Id}", transactionId);

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
                    $"PIX purchase confirmed: {transactionId}",
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

                if (activePackage is null)
                {
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
}