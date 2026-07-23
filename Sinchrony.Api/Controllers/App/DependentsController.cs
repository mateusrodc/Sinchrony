using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("students/me/dependents")]
[Produces("application/json")]
public class DependentsController(
    IDependentRepository dependentRepository,
    IStudentPackageRepository studentPackageRepository,
    IUserRepository userRepository,
    IPasswordService passwordService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private static object MapDependent(Dependent d) => new
    {
        id = d.Id,
        userId = d.UserId,
        name = d.Name,
        email = d.User?.Email,
        phone = d.User?.Phone,
        birthDate = d.BirthDate?.ToString("yyyy-MM-dd"),
        cpf = d.Cpf,
        canBook = d.CanBook,
        canCancel = d.CanCancel,
        canViewHistory = d.CanViewHistory,
        active = d.Active,
        responsibleStudentId = d.ResponsibleStudentId,
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

        // Valida email obrigatório
        if (string.IsNullOrEmpty(req.email))
            throw DomainException.Validation("EMAIL_REQUIRED", "Email é obrigatório para o dependente.");

        // Verifica se email já existe
        var existing = await userRepository.GetByEmailAsync(req.email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email já cadastrado.");

        // Busca o responsável para herdar UnitId
        var responsible = await userRepository.GetByIdAsync(UserId, ct)
            ?? throw DomainException.NotFound("Responsible student not found.");

        // Cria User para o dependente
        var hash = passwordService.HashPassword(Guid.NewGuid().ToString());
        var dependentUser = Domain.Entities.User.Create(req.name, req.email, req.phone, hash, Role.student,
            string.IsNullOrEmpty(req.cpf) ? null : req.cpf);

        // Herda unidade do responsável
        if (responsible.UnitId.HasValue)
            dependentUser.SetUnit(responsible.UnitId.Value);

        await userRepository.AddAsync(dependentUser, ct);
        await userRepository.SaveAsync(ct);

        // Cria o Dependent vinculado ao User
        var dependent = Dependent.Create(UserId, req.name,
            string.IsNullOrEmpty(req.birthDate) ? null : DateOnly.Parse(req.birthDate),
            req.cpf);
        dependent.LinkUser(dependentUser.Id);

        await dependentRepository.AddAsync(dependent, ct);
        await dependentRepository.SaveAsync(ct);

        // Recarrega com User incluído
        var created = await dependentRepository.GetByIdAsync(dependent.Id, ct);
        return StatusCode(201, MapDependent(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDependentRequest req, CancellationToken ct)
    {
        var dependent = await dependentRepository.GetByIdAsync(id, ct);
        if (dependent is null || dependent.ResponsibleStudentId != UserId)
            throw DomainException.NotFound("Dependent not found.");

        // Atualiza o User vinculado
        if (dependent.UserId.HasValue)
        {
            var dependentUser = await userRepository.GetByIdAsync(dependent.UserId.Value, ct);
            if (dependentUser is not null)
            {
                dependentUser.UpdateProfile(
                    req.name,
                    req.email ?? dependentUser.Email,
                    req.phone ?? dependentUser.Phone,
                    dependentUser.Avatar);
                await userRepository.SaveAsync(ct);
            }
        }

        dependent.Update(req.name,
            string.IsNullOrEmpty(req.birthDate) ? null : DateOnly.Parse(req.birthDate),
            req.cpf, req.canBook ?? true, req.canCancel ?? true,
            req.canViewHistory ?? true, req.active ?? true);

        await dependentRepository.SaveAsync(ct);

        var updated = await dependentRepository.GetByIdAsync(id, ct);
        return Ok(MapDependent(updated!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var dependent = await dependentRepository.GetByIdAsync(id, ct);
        if (dependent is null || dependent.ResponsibleStudentId != UserId)
            throw DomainException.NotFound("Dependent not found.");

        dependent.Deactivate();

        // Desativa o User vinculado também
        if (dependent.UserId.HasValue)
        {
            var dependentUser = await userRepository.GetByIdAsync(dependent.UserId.Value, ct);
            dependentUser?.Deactivate();
            await userRepository.SaveAsync(ct);
        }

        await dependentRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }
}

public record CreateDependentRequest(
    string name, string email, string? phone,
    string? birthDate, string? cpf);

public record UpdateDependentRequest(
    string name, string? email, string? phone,
    string? birthDate, string? cpf,
    bool? canBook, bool? canCancel, bool? canViewHistory, bool? active);