using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Api.Controllers.Erp;

// Controller próprio (e não o ErpReportsController, que é admin-only): aniversariantes é aberto a
// admin e professor, como a lista de alunos.
[Authorize(Roles = "admin,teacher")]
[ApiController]
[Route("api/reports/birthdays")]
[Produces("application/json")]
public class ErpBirthdaysReportController(
    IUserRepository userRepository,
    IUnitContext unitContext) : ControllerBase
{
    // Aniversariantes do mês, ordenados por dia. Mesmo escopo de unidade da lista de alunos:
    // admin global (ou sem unidade no token) vê todos; os demais, só os da própria unidade.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? month, CancellationToken ct)
    {
        var m = month ?? DateTime.UtcNow.Month;
        if (m is < 1 or > 12)
            throw DomainException.Validation("INVALID_MONTH", "month deve estar entre 1 e 12.");

        Guid? unitId = unitContext.IsGlobalAdmin || !unitContext.UnitId.HasValue
            ? null
            : unitContext.UnitId.Value;

        var students = await userRepository.ListBirthdaysAsync(m, unitId, ct);

        var data = students.Select(u => new
        {
            studentId = u.Id,
            name = u.Name,
            birthDate = u.BirthDate!.Value.ToString("yyyy-MM-dd"),
            day = u.BirthDate!.Value.Day,
            month = u.BirthDate!.Value.Month,
            phone = u.Phone,
            email = u.Email,
            status = u.Status.ToString()
        });

        return Ok(new { month = m, total = students.Count, data });
    }
}
