using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("students/me/dependents")]
[Produces("application/json")]
public class DependentsController(
    IDependentRepository dependentRepository,
    IStudentPackageRepository studentPackageRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private static object MapDependent(Dependent d) => new
    {
        id = d.Id,
        name = d.Name,
        birthDate = d.BirthDate?.ToString("yyyy-MM-dd"),
        cpf = d.Cpf,
        canBook = d.CanBook,
        canCancel = d.CanCancel,
        canViewHistory = d.CanViewHistory,
        active = d.Active,
        createdAt = d.CreatedAt
    };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var dependents = await dependentRepository.ListByStudentAsync(UserId, ct);
        return Ok(new { data = dependents.Select(MapDependent) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDependentRequest req, CancellationToken ct)
    {
        // Valida limite de dependentes pelo pacote ativo
        var activePackage = await studentPackageRepository.GetActiveByStudentAsync(UserId, ct);
        if (activePackage is not null)
        {
            var maxDependents = activePackage.Package?.MaxDependents ?? 0;
            var current = (await dependentRepository.ListByStudentAsync(UserId, ct)).Count();
            if (current >= maxDependents)
                throw DomainException.Validation("MAX_DEPENDENTS_REACHED",
                    $"Limite de {maxDependents} dependente(s) atingido para este pacote.");
        }

        var dependent = Dependent.Create(UserId, req.name,
            string.IsNullOrEmpty(req.birthDate) ? null : DateOnly.Parse(req.birthDate),
            req.cpf);

        await dependentRepository.AddAsync(dependent, ct);
        await dependentRepository.SaveAsync(ct);
        return StatusCode(201, MapDependent(dependent));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDependentRequest req, CancellationToken ct)
    {
        var dependent = await dependentRepository.GetByIdAsync(id, ct);
        if (dependent is null || dependent.ResponsibleStudentId != UserId)
            throw DomainException.NotFound("Dependent not found.");

        dependent.Update(req.name,
            string.IsNullOrEmpty(req.birthDate) ? null : DateOnly.Parse(req.birthDate),
            req.cpf, req.canBook ?? true, req.canCancel ?? true,
            req.canViewHistory ?? true, req.active ?? true);

        await dependentRepository.SaveAsync(ct);
        return Ok(MapDependent(dependent));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var dependent = await dependentRepository.GetByIdAsync(id, ct);
        if (dependent is null || dependent.ResponsibleStudentId != UserId)
            throw DomainException.NotFound("Dependent not found.");

        dependent.Deactivate();
        await dependentRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }
}

public record CreateDependentRequest(string name, string? birthDate, string? cpf);
public record UpdateDependentRequest(string name, string? birthDate, string? cpf,
    bool? canBook, bool? canCancel, bool? canViewHistory, bool? active);