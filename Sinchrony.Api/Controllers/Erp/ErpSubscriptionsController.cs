using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

// Status de pagamento dos pacotes recorrentes pro ERP — ESPECIFICACAO_API_STATUS_ASSINATURAS_V2.md.
[Authorize(Roles = "admin,teacher")]
[ApiController]
[Produces("application/json")]
public class ErpSubscriptionsController(
    IStudentPackageRepository studentPackageRepository,
    IPurchaseRepository purchaseRepository,
    ICardRepository cardRepository,
    IUserRepository userRepository,
    IAsaasService asaasService,
    IAuditService auditService,
    IUnitContext unitContext,
    RecurringRenewalService recurringRenewalService) : ControllerBase
{
    private const int MaxPageSize = 100;

    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

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

        // O cartão exibido tem que ser o que de fato é cobrado (RenewalCardId) — só cai pro
        // padrão do aluno quando a assinatura ainda não tem um definido.
        var studentIds = result.Items.Select(sp => sp.StudentId).Distinct().ToList();
        var renewalCardIds = result.Items
            .Where(sp => sp.RenewalCardId.HasValue)
            .Select(sp => sp.RenewalCardId!.Value).Distinct().ToList();
        var renewalCards = (await cardRepository.ListByIdsAsync(renewalCardIds, ct)).ToDictionary(c => c.Id);
        var defaultCards = (await cardRepository.ListDefaultByUserIdsAsync(studentIds, ct))
            .ToDictionary(c => c.UserId);

        Card? ResolveCard(StudentPackage sp) =>
            (sp.RenewalCardId.HasValue ? renewalCards.GetValueOrDefault(sp.RenewalCardId.Value) : null)
            ?? defaultCards.GetValueOrDefault(sp.StudentId);

        var data = result.Items.Select(sp => MapListItem(sp, ResolveCard(sp)));

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

        var card = await ResolveRenewalCardAsync(sp, ct);
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

        var card = await ResolveRenewalCardAsync(sp, ct);
        return Ok(new { data = MapListItem(sp, card) });
    }

    public record CancelSubscriptionRequest(string? reason);

    // Só o admin cancela a renovação automática (DEMANDA_CANCELAMENTO_RENOVACAO_SO_ADMIN_BACKEND.md)
    // — o aluno não tem mais essa rota, fala com o estúdio. O pacote continua válido até o
    // EndDate normalmente, só deixa de gerar o próximo ciclo.
    [HttpPost("api/students/{id}/subscription/cancel")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> CancelSubscription(
        Guid id, [FromBody] CancelSubscriptionRequest? request, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Aluno não encontrado.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        var sp = await studentPackageRepository.GetSubscriptionByStudentAsync(id, ct)
            ?? throw DomainException.NotFound("Aluno não possui assinatura recorrente.");

        if (!sp.AutoRenew)
            throw DomainException.Validation("RENEWAL_ALREADY_CANCELLED",
                "A renovação automática já está cancelada.");

        // Legado: se ainda vem de uma assinatura Asaas, encerra a cobrança por lá primeiro — o
        // modelo atual (AutoRenew) não depende da Asaas pra isso, é só cobrança direta no cartão.
        if (sp.AsaasSubscriptionId is not null)
            await asaasService.CancelSubscriptionAsync(sp.AsaasSubscriptionId, ct);

        sp.CancelRenewal();
        await studentPackageRepository.SaveAsync(ct);

        // Diferente do antigo cancelamento pelo aluno: aqui NÃO desbloqueia automaticamente quem
        // estava bloqueado por payment_failed — o admin decide caso a caso pelo toggle da ficha.
        var reason = string.IsNullOrWhiteSpace(request?.reason) ? null : request.reason.Trim();
        await auditService.LogAsync(
            "subscription.cancelled_by_admin", "StudentPackage", sp.Id, AdminId,
            reason is null ? $"StudentId: {id}" : $"StudentId: {id}, Motivo: {reason}",
            ct: ct);

        var card = await ResolveRenewalCardAsync(sp, ct);
        return Ok(new { data = MapListItem(sp, card) });
    }

    // O cartão exibido tem que ser o que de fato é cobrado (RenewalCardId) — só cai pro padrão
    // do aluno quando a assinatura ainda não tem um definido.
    private async Task<Card?> ResolveRenewalCardAsync(StudentPackage sp, CancellationToken ct)
    {
        if (sp.RenewalCardId.HasValue)
        {
            var renewalCard = await cardRepository.GetByIdAsync(sp.RenewalCardId.Value, ct);
            if (renewalCard is not null) return renewalCard;
        }

        return (await cardRepository.ListByUserAsync(sp.StudentId, ct)).FirstOrDefault(c => c.IsDefault);
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
