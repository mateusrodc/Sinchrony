using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/teachers")]
[Produces("application/json")]
public class ErpTeachersController(
    IUserRepository userRepository,
    ITeacherUnitRepository teacherUnitRepository,
    IPasswordService passwordService,
    IUnitContext unitContext) : ControllerBase
{
    private static object MapTeacher(User u) => new
    {
        id = u.Id,
        name = u.Name,
        email = u.Email,
        cpf = u.Cpf,
        phone = u.Phone,
        active = u.Active,
        avatar = u.Avatar,
        unitIds = u.TeacherUnits.Select(tu => tu.UnitId).ToList(),
        units = u.TeacherUnits.Select(tu => new { id = tu.UnitId, name = tu.Unit?.Name }).ToList(),
        specialties = string.IsNullOrEmpty(u.Specialties)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(u.Specialties),
        cep = u.Cep,
        logradouro = u.Logradouro,
        numero = u.Numero,
        complemento = u.Complemento,
        bairro = u.Bairro,
        cidade = u.Cidade,
        estado = u.Estado
    };

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? unitId,
        [FromQuery] bool? active,
        CancellationToken ct)
    {
        var teachers = await userRepository.ListTeachersAsync(null, ct);

        // Filtro por unidade
        var filterUnitId = unitId ?? (!unitContext.IsGlobalAdmin ? unitContext.UnitId : null);
        if (filterUnitId.HasValue)
            teachers = teachers.Where(t =>
                t.TeacherUnits.Any(tu => tu.UnitId == filterUnitId.Value));

        if (active.HasValue)
            teachers = teachers.Where(t => t.Active == active.Value);

        return Ok(new { data = teachers.Select(MapTeacher) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct);
        if (teacher is null || teacher.Role != Role.teacher)
            throw DomainException.NotFound("Teacher not found.");
        return Ok(MapTeacher(teacher));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeacherRequest req, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(req.email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        if (!string.IsNullOrEmpty(req.cpf))
        {
            if (!CpfValidator.IsValid(req.cpf))
                throw DomainException.Validation("INVALID_CPF", "CPF inválido.");
            var cpfInUse = await userRepository.GetByCpfAsync(CpfValidator.Sanitize(req.cpf), ct);
            if (cpfInUse is not null)
                throw DomainException.Conflict("CPF_ALREADY_IN_USE", "CPF já cadastrado.");
        }

        var hash = passwordService.HashPassword(req.password);
        var teacher = Domain.Entities.User.Create(req.name, req.email, req.phone, hash, Role.teacher,
            string.IsNullOrEmpty(req.cpf) ? null : CpfValidator.Sanitize(req.cpf));

        teacher.UpdateSpecialties(req.specialties);
        teacher.UpdateAddress(req.cep, req.logradouro, req.numero,
            req.complemento, req.bairro, req.cidade, req.estado);

        if (!req.active) teacher.Deactivate();

        await userRepository.AddAsync(teacher, ct);
        await userRepository.SaveAsync(ct);

        // Vincula às unidades
        var unitIds = req.unitIds ?? [];
        if (!unitIds.Any() && unitContext.UnitId.HasValue)
            unitIds = [unitContext.UnitId.Value];

        if (unitIds.Any())
        {
            await teacherUnitRepository.UpdateTeacherUnitsAsync(teacher.Id, unitIds, ct);
            await teacherUnitRepository.SaveAsync(ct);
        }

        var created = await userRepository.GetByIdAsync(teacher.Id, ct);
        return StatusCode(201, MapTeacher(created!));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeacherRequest req, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct);
        if (teacher is null || teacher.Role != Role.teacher)
            throw DomainException.NotFound("Teacher not found.");

        if (!string.IsNullOrEmpty(req.cpf))
        {
            if (!CpfValidator.IsValid(req.cpf))
                throw DomainException.Validation("INVALID_CPF", "CPF inválido.");
            var cpfInUse = await userRepository.GetByCpfAsync(CpfValidator.Sanitize(req.cpf), ct);
            if (cpfInUse is not null && cpfInUse.Id != id)
                throw DomainException.Conflict("CPF_ALREADY_IN_USE", "CPF já cadastrado.");
            teacher.UpdateCpf(req.cpf);
        }

        teacher.UpdateProfile(req.name, req.email, req.phone, teacher.Avatar);
        teacher.UpdateSpecialties(req.specialties);
        teacher.UpdateAddress(req.cep, req.logradouro, req.numero,
            req.complemento, req.bairro, req.cidade, req.estado);

        if (req.active == false) teacher.Deactivate();
        else if (req.active == true) teacher.Reactivate();

        await userRepository.SaveAsync(ct);

        // Atualiza unidades se enviado
        if (req.unitIds is not null)
        {
            await teacherUnitRepository.UpdateTeacherUnitsAsync(id, req.unitIds, ct);
            await teacherUnitRepository.SaveAsync(ct);
        }

        var updated = await userRepository.GetByIdAsync(id, ct);
        return Ok(MapTeacher(updated!));
    }
}

public record CreateTeacherRequest(
    string name, string email, string? phone,
    string password, bool active,
    string? cpf = null,
    List<string>? specialties = null,
    List<Guid>? unitIds = null,
    string? cep = null, string? logradouro = null, string? numero = null,
    string? complemento = null, string? bairro = null, string? cidade = null,
    string? estado = null);

public record UpdateTeacherRequest(
    string name, string email, string? phone,
    bool? active = null,
    string? cpf = null,
    List<string>? specialties = null,
    List<Guid>? unitIds = null,
    string? cep = null, string? logradouro = null, string? numero = null,
    string? complemento = null, string? bairro = null, string? cidade = null,
    string? estado = null);