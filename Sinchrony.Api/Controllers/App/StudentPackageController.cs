using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Interfaces.Repositories;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Produces("application/json")]
public class StudentPackageController(
    IStudentPackageRepository studentPackageRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private static object MapStudentPackage(Domain.Entities.StudentPackage sp) => new
    {
        id = sp.Id,
        packageId = sp.PackageId,
        packageName = sp.Package?.Name,
        packageType = sp.Package?.PackageType?.Name,
        status = sp.Status.ToString(),
        purchasedAt = sp.PurchasedAt,
        startDate = sp.StartDate,
        endDate = sp.EndDate,
        allocations = sp.Allocations.Select(a => new
        {
            dependentId = a.DependentId,
            creditsRemaining = a.CreditsRemaining
        })
    };

    [HttpGet("students/me/package")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var sp = await studentPackageRepository.GetActiveByStudentAsync(UserId, ct);
        if (sp is null) return NotFound(new { message = "No active package." });
        return Ok(MapStudentPackage(sp));
    }

    [HttpGet("api/students/{id}/packages")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "admin")]
    public async Task<IActionResult> ListByStudent(Guid id, CancellationToken ct)
    {
        var packages = await studentPackageRepository.ListByStudentAsync(id, ct);
        return Ok(new { data = packages.Select(MapStudentPackage) });
    }
}