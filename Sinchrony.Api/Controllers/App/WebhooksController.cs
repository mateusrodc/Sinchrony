using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Text.Json;

namespace Sinchrony.Api.Controllers.App;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    IPurchaseRepository purchaseRepository,
    IUserRepository userRepository,
    ICreditTransactionRepository creditTransactionRepository,
    IAuditService auditService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("asaas")]
    public async Task<IActionResult> Asaas([FromBody] JsonElement payload, CancellationToken ct)
    {
        // Valida token de segurança do webhook
        var webhookToken = configuration["Asaas:WebhookToken"];
        if (!string.IsNullOrEmpty(webhookToken))
        {
            Request.Headers.TryGetValue("asaas-access-token", out var receivedToken);
            if (receivedToken != webhookToken)
                return Unauthorized();
        }

        var eventType = payload.GetProperty("event").GetString();
        if (eventType is not ("PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED"))
            return Ok(); // ignorar outros eventos

        var payment = payload.GetProperty("payment");
        var transactionId = payment.GetProperty("id").GetString()!;

        // Idempotência — verifica se já foi processado
        var purchases = await purchaseRepository.ListAllAsync(ct);
        var alreadyProcessed = purchases.Any(p =>
            p.TransactionId == transactionId && p.Status == "confirmed");

        if (alreadyProcessed)
            return Ok(new { message = "Already processed." });

        // Atualiza compras pendentes com esse transactionId
        var pendingPurchases = purchases
            .Where(p => p.TransactionId == transactionId && p.Status == "pending")
            .ToList();

        if (!pendingPurchases.Any())
            return Ok(new { message = "No pending purchase found." });

        foreach (var purchase in pendingPurchases)
        {
            purchase.Confirm();

            var user = await userRepository.GetByIdAsync(purchase.UserId, ct);
            if (user is null) continue;

            // Busca o pacote para saber quantos créditos dar
            var credits = purchase.Package?.Credits ?? 0;
            if (credits > 0)
            {
                user.AddCredits(credits);

                var creditTx = CreditTransaction.Create(
                    user.Id, credits, user.Credits,
                    $"Purchase confirmed: {transactionId}",
                    "purchase", purchase.Id);
                await creditTransactionRepository.AddAsync(creditTx, ct);
            }

            await auditService.LogAsync(
                "payment.confirmed", "Purchase",
                purchase.Id, purchase.UserId,
                $"TransactionId: {transactionId}, Event: {eventType}", ct: ct);
        }

        await purchaseRepository.SaveAsync(ct);
        await userRepository.SaveAsync(ct);
        await creditTransactionRepository.SaveAsync(ct);

        return Ok(new { message = "Processed." });
    }
}