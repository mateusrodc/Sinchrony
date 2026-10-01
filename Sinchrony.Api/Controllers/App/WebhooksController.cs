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
            var paymentConfirmationService = scope.ServiceProvider.GetRequiredService<IPaymentConfirmationService>();

            try
            {
                await ProcessWebhookAsync(payloadCopy, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository,
                    auditSvc, asaasService, recurringRenewalService, paymentConfirmationService, CancellationToken.None);
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
        IPaymentConfirmationService paymentConfirmationService,
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

        // A Asaas não expõe um campo dedicado de "motivo da recusa" nesses eventos.
        // payment.description é só o texto que o Sinchrony mesmo mandou ao criar a cobrança
        // ("4Sinchrony - X (renovação)") — usá-lo como motivo seria enganoso, melhor null.
        string? failureReason = null;

        logger.LogInformation("Asaas webhook processing transactionId: {Id}", transactionId);

        switch (eventType)
        {
            case "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED":
                await paymentConfirmationService.ConfirmPendingAsync(transactionId, eventType!, ct);
                break;

            case "PAYMENT_REPROVED_BY_RISK_ANALYSIS" or "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED":
                await HandleAdHocPaymentRefusedAsync(
                    transactionId, eventType!, failureReason, purchaseRepository, userRepository,
                    creditTransactionRepository, studentPackageRepository, auditSvc,
                    recurringRenewalService, ct);
                break;
        }
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
