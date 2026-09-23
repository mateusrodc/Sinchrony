using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Services;

namespace Sinchrony.Api.Controllers.Erp;

// Status de pagamento dos pacotes recorrentes pro ERP — ESPECIFICACAO_API_STATUS_ASSINATURAS_V2.md.
[Authorize(Roles = "admin,teacher")]
[ApiController]
[Produces("application/json")]
public class ErpSubscriptionsController(
    IStudentPackageRepository studentPackageRepository,
    IPurchaseRepository purchaseRepository,
    ICardRepository cardRepository,
    IUnitContext unitContext,
    RecurringRenewalService recurringRenewalService) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet("api/subscriptions")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] Guid? unitId,
        [FromQuery] DateOnly? dueFrom,
        [FromQuery] DateOnly? dueTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Falha fechado: quem não é admin global só enxerga a própria unidade, igual às demais
        // telas financeiras do ERP (ver ErpPurchasesController).
        Guid? effectiveUnitId = unitId;
        if (!unitContext.IsGlobalAdmin)
        {
            if (!unitContext.UnitId.HasValue)
                return Forbid();
            effectiveUnitId = unitContext.UnitId.Value;
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var filter = new SubscriptionListFilter(status, search, effectiveUnitId, dueFrom, dueTo);
        var result = await studentPackageRepository.ListSubscriptionsPagedAsync(filter, page, pageSize, ct);

        var studentIds = result.Items.Select(sp => sp.StudentId).Distinct().ToList();
        var defaultCards = (await cardRepository.ListDefaultByUserIdsAsync(studentIds, ct))
            .ToDictionary(c => c.UserId);

        var data = result.Items.Select(sp =>
            MapListItem(sp, defaultCards.GetValueOrDefault(sp.StudentId)));

        var paged = PagedResult.Create(data, page, pageSize, result.Total);
        return Ok(new
        {
            paged.Data,
            paged.Pagination,
            summary = new
            {
                upToDate = result.Summary.UpToDate,
                retrying = result.Summary.Retrying,
                overdue = result.Summary.Overdue,
                cancelled = result.Summary.Cancelled,
                expectedMonthlyRevenue = result.Summary.ExpectedMonthlyRevenue,
                overdueAmount = result.Summary.OverdueAmount
            }
        });
    }

    [HttpGet("api/students/{id}/subscription")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetStudentSubscription(Guid id, CancellationToken ct)
    {
        var sp = await studentPackageRepository.GetSubscriptionByStudentAsync(id, ct)
            ?? throw DomainException.NotFound("Aluno não possui assinatura recorrente.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && sp.Student?.UnitId != unitContext.UnitId)
            return Forbid();

        var card = (await cardRepository.ListByUserAsync(id, ct)).FirstOrDefault(c => c.IsDefault);
        var (payments, _) = await purchaseRepository.ListByStudentPackagePagedAsync(sp.Id, 1, 6, ct);

        return Ok(new { data = MapDetail(sp, card, payments) });
    }

    [HttpGet("api/students/{id}/subscription/payments")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetStudentSubscriptionPayments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var sp = await studentPackageRepository.GetSubscriptionByStudentAsync(id, ct)
            ?? throw DomainException.NotFound("Aluno não possui assinatura recorrente.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && sp.Student?.UnitId != unitContext.UnitId)
            return Forbid();

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await purchaseRepository.ListByStudentPackagePagedAsync(sp.Id, page, pageSize, ct);
        return Ok(PagedResult.Create(items.Select(MapPayment), page, pageSize, total));
    }

    // Se há uma renovação "pending", confere o status real na Asaas; senão, se o pacote está em
    // retentativa/vencido, tenta cobrar agora — sem esperar o próximo tick do job (a cada 5min).
    [HttpPost("api/subscriptions/{studentPackageId}/sync")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> Sync(Guid studentPackageId, CancellationToken ct)
    {
        var before = await studentPackageRepository.GetByIdAsync(studentPackageId, ct)
            ?? throw DomainException.NotFound("Pacote não encontrado.");

        if (!before.AutoRenew && before.AsaasSubscriptionId is null)
            throw DomainException.Validation("NOT_RECURRING", "Este pacote não é recorrente.");

        await recurringRenewalService.SyncAsync(studentPackageId, ct);

        var sp = await studentPackageRepository.GetByIdAsync(studentPackageId, ct)
            ?? throw DomainException.NotFound("Pacote não encontrado.");

        var card = (await cardRepository.ListByUserAsync(sp.StudentId, ct)).FirstOrDefault(c => c.IsDefault);
        return Ok(new { data = MapListItem(sp, card) });
    }

    private static object MapListItem(StudentPackage sp, Card? card) => new
    {
        studentPackageId = sp.Id,
        studentId = sp.StudentId,
        studentName = sp.Student?.Name ?? string.Empty,
        packageId = sp.PackageId,
        packageName = sp.Package?.Name ?? string.Empty,
        paymentStatus = sp.PaymentStatus?.ToString(),
        amount = sp.Package?.Price,
        nextDueDate = BrasiliaTime.ToDate(sp.EndDate),
        lastPaidAt = sp.LastPaidAt,
        lastPaidAmount = sp.LastPaidAmount,
        problemSince = sp.ProblemSince,
        lastFailureReason = sp.LastFailureReason,
        cardBrand = card?.Brand,
        cardLastDigits = card?.LastDigits,
        lastSyncedAt = sp.LastSyncedAt
    };

    private static object MapDetail(StudentPackage sp, Card? card, IEnumerable<Purchase> payments) => new
    {
        studentPackageId = sp.Id,
        studentId = sp.StudentId,
        studentName = sp.Student?.Name ?? string.Empty,
        packageId = sp.PackageId,
        packageName = sp.Package?.Name ?? string.Empty,
        paymentStatus = sp.PaymentStatus?.ToString(),
        amount = sp.Package?.Price,
        nextDueDate = BrasiliaTime.ToDate(sp.EndDate),
        lastPaidAt = sp.LastPaidAt,
        lastPaidAmount = sp.LastPaidAmount,
        problemSince = sp.ProblemSince,
        lastFailureReason = sp.LastFailureReason,
        cardBrand = card?.Brand,
        cardLastDigits = card?.LastDigits,
        lastSyncedAt = sp.LastSyncedAt,
        payments = payments.Select(MapPayment)
    };

    // Purchase não guarda a data de vencimento (só CreatedAt, quando a cobrança foi processada)
    // — dueDate aqui aproxima pela data de criação da Purchase, suficiente pro histórico.
    private static object MapPayment(Purchase p) => new
    {
        purchaseId = p.Id,
        transactionId = p.TransactionId,
        amount = p.Amount,
        status = p.Status,
        dueDate = DateOnly.FromDateTime(p.CreatedAt),
        createdAt = p.CreatedAt
    };
}
