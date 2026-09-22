using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin,teacher")]
[ApiController]
[Route("api/purchases")]
[Produces("application/json")]
public class ErpPurchasesController(
    IPurchaseRepository purchaseRepository,
    IUnitContext unitContext) : ControllerBase
{
    private const int MaxPageSize = 100;
    private static readonly string[] ValidStatuses = ["pending", "confirmed", "failed"];
    private static readonly string[] ValidPaymentMethods = ["pix", "card"];

    // Tela de acompanhamento (não é módulo financeiro): quem comprou o quê, quando e o status do pagamento.
    // Todo filtro é opcional. `summary` reflete o recorte filtrado, não o total do sistema.
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpPurchaseListResponseExample))]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] string? paymentMethod,
        [FromQuery] string? studentSearch,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Falha fechado: quem não é admin global e não tem unidade no token não pode
        // enxergar valores financeiros de todas as unidades.
        Guid? unitId = null;
        if (!unitContext.IsGlobalAdmin)
        {
            if (!unitContext.UnitId.HasValue)
                return Forbid();
            unitId = unitContext.UnitId.Value;
        }

        var normalizedStatus = status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedStatus) && !ValidStatuses.Contains(normalizedStatus))
            throw new DomainException("INVALID_STATUS", "Status deve ser 'pending', 'confirmed' ou 'failed'.");

        var normalizedMethod = paymentMethod?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedMethod) && !ValidPaymentMethods.Contains(normalizedMethod))
            throw new DomainException("INVALID_PAYMENT_METHOD", "paymentMethod deve ser 'pix' ou 'card'.");

        var fromUtc = from.HasValue ? ToUtc(from.Value) : (DateTime?)null;
        DateTime? toExclusiveUtc = null;
        if (to.HasValue)
        {
            var toUtc = ToUtc(to.Value);
            // Data pura (00:00) significa "até o fim desse dia"; com horário, o limite é exato.
            toExclusiveUtc = toUtc.TimeOfDay == TimeSpan.Zero ? toUtc.AddDays(1) : toUtc.AddTicks(1);
        }

        if (fromUtc.HasValue && toExclusiveUtc.HasValue && fromUtc >= toExclusiveUtc)
            throw new DomainException("INVALID_DATE_RANGE", "'from' deve ser anterior a 'to'.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var filter = new PurchaseListFilter(
            fromUtc, toExclusiveUtc, normalizedStatus, normalizedMethod, studentSearch, unitId);
        var result = await purchaseRepository.ListErpPagedAsync(filter, page, pageSize, ct);

        var data = result.Items.Select(p => new
        {
            id = p.Id,
            studentId = p.UserId,
            studentName = p.User?.Name ?? string.Empty,
            packageId = p.PackageId,
            packageName = p.Package?.Name ?? string.Empty,
            amount = p.Amount,
            paymentMethod = p.PaymentMethod,
            status = p.Status,
            transactionId = p.TransactionId,
            isRecurring = p.Package?.IsRecurring ?? false,
            createdAt = p.CreatedAt
        });

        var paged = PagedResult.Create(data, page, pageSize, result.Total);
        return Ok(new
        {
            paged.Data,
            paged.Pagination,
            summary = new
            {
                count = result.Total,
                totalAmount = result.TotalAmount,
                // Sem filtro de status, totalAmount inclui pendentes; este é o que de fato entrou.
                confirmedAmount = result.ConfirmedAmount
            }
        });
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
