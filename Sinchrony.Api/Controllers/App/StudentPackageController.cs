using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.App;
using Sinchrony.Api.SwaggerExamples.Erp;
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
    IStudentPackageRepository studentPackageRepository, IPackageRepository packageRepository, IAuditService auditService) : ControllerBase
{
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