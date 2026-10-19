using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Api.Controllers.Erp;

// Histórico comprovável do aceite dos Termos de Uso (DEMANDA_PROVA_DE_ACEITE_DOS_TERMOS_BACKEND.md).
[Authorize(Roles = "admin")]
[ApiController]
[Produces("application/json")]
public class ErpTermsAcceptancesController(
    IUserRepository userRepository,
    ITermsAcceptanceRepository termsAcceptanceRepository,
    IUnitContext unitContext) : ControllerBase
{
    [HttpGet("api/students/{id}/terms-acceptances")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Aluno não encontrado.");

        await unitContext.EnsureCanAccessUnitAsync(student.UnitId, ct);

        var items = await termsAcceptanceRepository.ListByUserAsync(id, ct);

        return Ok(new
        {
            data = items.Select(t => new
            {
                id = t.Id,
                termsVersion = t.TermsVersion,
                acceptedAt = t.AcceptedAt,
                clientAcceptedAt = t.ClientAcceptedAt,
                channel = t.Channel,
                ipAddress = t.IpAddress,
                userAgent = t.UserAgent,
                appVersion = t.AppVersion
            })
        });
    }
}
