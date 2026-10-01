using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.App;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Packages.Commands.UpdateSubscriptionCard;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Sinchrony.Infrastructure.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Produces("application/json")]
public class StudentPackageController(
    IStudentPackageRepository studentPackageRepository, IPackageRepository packageRepository,
    IAuditService auditService, IMediator mediator, IAsaasService asaasService,
    IPurchaseRepository purchaseRepository, ICardRepository cardRepository) : ControllerBase
{
    private const int MaxPageSize = 100;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private static object MapStudentPackage(Domain.Entities.StudentPackage sp) => new
    {
        id = sp.Id,
        packageId = sp.PackageId,
        packageName = sp.Package?.Name,
        packageType = sp.Package?.PackageType?.Name,
        status = sp.Status.ToString(),
        source = sp.Source,
        creditsGranted = sp.CreditsGranted,
        purchasedAt = sp.PurchasedAt,
        startDate = sp.StartDate,
        endDate = sp.EndDate,
        // Renovação automática (DEMANDA_RECORRENCIA...V2.md §4.4) — nextDueDate é o próprio
        // EndDate em horário de Brasília, é quando a próxima cobrança acontece.
        autoRenew = sp.AutoRenew,
        paymentStatus = sp.PaymentStatus?.ToString(),
        nextDueDate = sp.AutoRenew ? Application.Common.BrasiliaTime.ToDate(sp.NextRenewalDueAt) : (DateOnly?)null,
        allocations = sp.Allocations.Select(a => new
        {
            dependentId = a.DependentId,
            creditsRemaining = a.CreditsRemaining
        })
    };
    public record ExtendPackageRequest(int days, string reason);
    public record CancelRefundRequest(decimal refundAmount, string reason);

    [HttpGet("students/me/package")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(StudentPackageResponseExample))]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var sp = await studentPackageRepository.GetActiveByStudentAsync(UserId, ct);
        if (sp is null) return NotFound(new { message = "No active package." });
        return Ok(MapStudentPackage(sp));
    }

    public record UpdateSubscriptionCardRequest(Guid cardId);

    // Aluno bloqueado por falha de pagamento (ou querendo trocar o cartão preventivamente)
    // atualiza o cartão usado nas renovações automáticas. Se estava retrying/overdue, o job de
    // renovação tenta cobrar de novo já no próximo tick — não desbloqueia na hora.
    [HttpPatch("students/me/subscription/card")]
    public async Task<IActionResult> UpdateSubscriptionCard(
        [FromBody] UpdateSubscriptionCardRequest req, CancellationToken ct)
    {
        await mediator.Send(new UpdateSubscriptionCardCommand(UserId, req.cardId), ct);

        await auditService.LogAsync(
            "subscription.card_updated", "User", UserId, UserId,
            $"CardId: {req.cardId}", ct: ct);

        return Ok(new { message = "Cartão da assinatura atualizado. A próxima cobrança usará o novo cartão." });
    }

    // Cancelamento da renovação automática saiu do App do aluno — quem cancela agora é só o
    // admin (DEMANDA_CANCELAMENTO_RENOVACAO_SO_ADMIN_BACKEND.md), via
    // POST /api/students/{id}/subscription/cancel no ErpSubscriptionsController.

    // Status de pagamento da própria assinatura — mesmo formato do endpoint admin
    // (GET /api/students/{id}/subscription), sem lastSyncedAt (detalhe interno de reconciliação).
    // O aluno vê o histórico completo dos últimos ciclos; os anteriores vêm do endpoint paginado.
    [HttpGet("students/me/subscription")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetMySubscription(CancellationToken ct)
    {
        var sp = await studentPackageRepository.GetSubscriptionByStudentAsync(UserId, ct)
            ?? throw DomainException.NotFound("Você não possui uma assinatura recorrente.");

        var card = await ResolveRenewalCardAsync(sp, ct);
        var (payments, _) = await purchaseRepository.ListByStudentPackagePagedAsync(sp.Id, 1, 6, ct);

        return Ok(new { data = MapSubscription(sp, card, payments) });
    }

    // O cartão exibido tem que ser o que de fato é cobrado (RenewalCardId) — só cai pro padrão
    // do aluno quando a assinatura ainda não tem um definido.
    private async Task<Domain.Entities.Card?> ResolveRenewalCardAsync(
        Domain.Entities.StudentPackage sp, CancellationToken ct)
    {
        if (sp.RenewalCardId.HasValue)
        {
            var renewalCard = await cardRepository.GetByIdAsync(sp.RenewalCardId.Value, ct);
            if (renewalCard is not null) return renewalCard;
        }

        return (await cardRepository.ListByUserAsync(sp.StudentId, ct)).FirstOrDefault(c => c.IsDefault);
    }

    [HttpGet("students/me/subscription/payments")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetMySubscriptionPayments(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var sp = await studentPackageRepository.GetSubscriptionByStudentAsync(UserId, ct)
            ?? throw DomainException.NotFound("Você não possui uma assinatura recorrente.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await purchaseRepository.ListByStudentPackagePagedAsync(sp.Id, page, pageSize, ct);
        return Ok(Application.Common.PagedResult.Create(items.Select(MapPayment), page, pageSize, total));
    }

    private static object MapSubscription(
        Domain.Entities.StudentPackage sp, Domain.Entities.Card? card, IEnumerable<Domain.Entities.Purchase> payments) => new
    {
        studentPackageId = sp.Id,
        packageId = sp.PackageId,
        packageName = sp.Package?.Name ?? string.Empty,
        paymentStatus = sp.PaymentStatus?.ToString(),
        amount = sp.Package?.Price,
        nextDueDate = Application.Common.BrasiliaTime.ToDate(sp.NextRenewalDueAt),
        lastPaidAt = sp.LastPaidAt,
        lastPaidAmount = sp.LastPaidAmount,
        problemSince = sp.ProblemSince,
        lastFailureReason = sp.LastFailureReason,
        cardBrand = card?.Brand,
        cardLastDigits = card?.LastDigits,
        payments = payments.Select(MapPayment)
    };

    // Purchase não guarda a data de vencimento — dueDate aproxima pela data de criação.
    private static object MapPayment(Domain.Entities.Purchase p) => new
    {
        purchaseId = p.Id,
        transactionId = p.TransactionId,
        amount = p.Amount,
        status = p.Status,
        dueDate = DateOnly.FromDateTime(p.CreatedAt),
        createdAt = p.CreatedAt
    };

    [HttpGet("api/students/{id}/packages")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(StudentPackagesErpResponseExample))]
    public async Task<IActionResult> ListByStudent(Guid id, CancellationToken ct)
    {
        var packages = await studentPackageRepository.ListByStudentAsync(id, ct);
        return Ok(new { data = packages.Select(MapStudentPackage) });
    }
    [HttpPost("{id}/extend")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Extend(
        Guid id,
        [FromBody] ExtendPackageRequest req,
        CancellationToken ct)
    {
        if (req.days <= 0)
            throw DomainException.Validation("INVALID_DAYS",
                "O número de dias deve ser maior que zero.");

        if (string.IsNullOrWhiteSpace(req.reason) || req.reason.Trim().Length < 3)
            throw DomainException.Validation("REASON_REQUIRED",
                "O motivo da extensão é obrigatório (mínimo 3 caracteres).");

        var sp = await studentPackageRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("StudentPackage not found.");

        if (sp.Status == Domain.Entities.StudentPackageStatus.cancelled)
            throw DomainException.Conflict("PACKAGE_ALREADY_CANCELLED",
                "Não é possível estender um pacote cancelado.");

        sp.ExtendValidity(req.days);
        await studentPackageRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "package.validity_extended", "StudentPackage",
            sp.Id, AdminId,
            $"Extensão de {req.days} dias. Motivo: {req.reason}. Nova validade: {sp.EndDate:yyyy-MM-dd}",
            ct: ct);

        return Ok(new
        {
            id = sp.Id,
            studentId = sp.StudentId,
            packageId = sp.PackageId,
            status = sp.Status.ToString(),
            endDate = sp.EndDate,
            daysExtended = req.days,
            reason = req.reason
        });
    }
    [HttpPost("{id}/cancel-refund")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CancelRefund(
        Guid id,
        [FromBody] CancelRefundRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.reason) || req.reason.Trim().Length < 3)
            throw DomainException.Validation("REASON_REQUIRED",
                "O motivo do cancelamento é obrigatório (mínimo 3 caracteres).");

        if (req.refundAmount < 0)
            throw DomainException.Validation("INVALID_REFUND_AMOUNT",
                "O valor de reembolso não pode ser negativo.");

        var sp = await studentPackageRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("StudentPackage not found.");

        if (sp.Status == Domain.Entities.StudentPackageStatus.cancelled)
            throw DomainException.Conflict("PACKAGE_ALREADY_CANCELLED",
                "Este pacote já está cancelado.");

        var package = await packageRepository.GetByIdAsync(sp.PackageId, ct);

        // Legado: se esse pacote ainda vem de uma assinatura Asaas, cancela por lá primeiro —
        // se falhar, não cancela localmente, senão a Asaas continua cobrando um pacote que o
        // ERP já mostra como cancelado. O modelo atual (AutoRenew) não precisa disso, Cancel()
        // já desliga a renovação localmente.
        if (sp.AsaasSubscriptionId is not null)
            await asaasService.CancelSubscriptionAsync(sp.AsaasSubscriptionId, ct);

        sp.Cancel();
        await studentPackageRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "package.cancelled_with_refund", "StudentPackage",
            sp.Id, AdminId,
            $"Cancelamento com reembolso. Pacote: {package?.Name}. Valor a reembolsar: R$ {req.refundAmount:F2}. Motivo: {req.reason}. Prazo: 30 dias úteis.",
            ct: ct);

        return Ok(new
        {
            id = sp.Id,
            studentId = sp.StudentId,
            packageId = sp.PackageId,
            packageName = package?.Name,
            status = "cancelled",
            refundAmount = req.refundAmount,
            refundDeadlineDays = 30,
            reason = req.reason,
            message = "Reembolso registrado. O financeiro deve processar em até 30 dias úteis."
        });
    }
}