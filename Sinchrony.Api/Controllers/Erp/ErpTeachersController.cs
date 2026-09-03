using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Sinchrony.Infrastructure.Services;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/teachers")]
[Produces("application/json")]
public class ErpTeachersController(
    IUserRepository userRepository,
    ITeacherUnitRepository teacherUnitRepository,
    IPasswordService passwordService,
    ISettingsRepository settingsRepository,
    IEmailService emailService,
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
        role = u.Role.ToString(),
        cargo = u.Cargo,
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

    // "role" só aceita teacher/admin — nunca student. Usado tanto na criação quanto na edição
    // (DEMANDA_CADASTRO_PROFESSOR_ACEITAR_PERFIL_BACKEND.md). Default "teacher" quando ausente,
    // pra não quebrar quem já chama o POST de hoje sem esse campo.
    private static Role ParseStaffRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Role.teacher;

        if (!Enum.TryParse<Role>(role, ignoreCase: true, out var parsed) || parsed == Role.student)
            throw DomainException.Validation("INVALID_ROLE",
                "Papel inválido. Use \"teacher\" ou \"admin\".");

        return parsed;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? unitId,
        [FromQuery] bool? active,
        [FromQuery] bool includeAdmins = false,
        CancellationToken ct = default)
    {
        // includeAdmins=false (padrão) preserva o seletor de professor da tela de aula — só
        // Role.teacher, comportamento idêntico ao de sempre. A tela "Cadastro de Usuários" passa
        // includeAdmins=true pra ver professores + administradores/secretárias juntos.
        var teachers = await userRepository.ListTeachersAsync(null, includeAdmins, ct);

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
        if (teacher is null || teacher.Role == Role.student)
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

        var role = ParseStaffRole(req.role);

        var hash = passwordService.HashPassword(req.password);
        var teacher = Domain.Entities.User.Create(req.name, req.email, req.phone, hash, role,
            string.IsNullOrEmpty(req.cpf) ? null : CpfValidator.Sanitize(req.cpf));

        teacher.UpdateSpecialties(req.specialties);
        teacher.UpdateCargo(req.cargo);
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
        if (teacher is null || teacher.Role == Role.student)
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

        if (!string.IsNullOrEmpty(req.role))
            teacher.SetRole(ParseStaffRole(req.role));

        teacher.UpdateProfile(req.name, req.email, req.phone, teacher.Avatar);
        teacher.UpdateSpecialties(req.specialties);
        teacher.UpdateCargo(req.cargo);
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
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct);
        if (teacher is null || teacher.Role == Role.student)
            throw DomainException.NotFound("Teacher not found.");

        teacher.Deactivate();
        await userRepository.SaveAsync(ct);
        return Ok(new { success = true, active = false });
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct);
        if (teacher is null || teacher.Role == Role.student)
            throw DomainException.NotFound("Teacher not found.");

        teacher.Reactivate();
        await userRepository.SaveAsync(ct);
        return Ok(new { success = true, active = true });
    }
    [HttpPost("{id}/send-password")]
    public async Task<IActionResult> SendPassword(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct);
        if (teacher is null || teacher.Role == Role.student)
            throw DomainException.NotFound("Teacher not found.");

        // Gera senha temporária
        var tempPassword = Guid.NewGuid().ToString("N")[..8].ToUpper();
        var hash = passwordService.HashPassword(tempPassword);
        teacher.ChangePassword(hash);
        await userRepository.SaveAsync(ct);

        // Tenta enviar por email em background (pode falhar no Render gratuito)
        var teacherEmail = teacher.Email;
        var teacherName = teacher.Name;
        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await settingsRepository.GetAsync(ct);
                var body = $"""
                <h2>Senha Temporária — 4Sinchrony</h2>
                <p>Olá, {teacherName}!</p>
                <p>Sua senha temporária é: <strong>{tempPassword}</strong></p>
                <p>Por favor, altere sua senha após o primeiro acesso.</p>
                <br><small>4Sinchrony Experience</small>
                """;
                await emailService.SendWithSettingsAsync(
                    teacherEmail, "Sua senha temporária — 4Sinchrony", body, settings,
                    CancellationToken.None);
            }
            catch { /* SMTP pode estar bloqueado no Render gratuito */ }
        });

        // Retorna a senha para o admin poder comunicar manualmente se o email falhar
        return Ok(new
        {
            success = true,
            temporaryPassword = tempPassword,
            message = "Senha temporária gerada. Se o email não chegar, use a senha retornada."
        });
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
    string? estado = null,
    // "teacher" ou "admin" — nunca "student". Ausente = "teacher" (compat com quem já chama
    // sem esse campo).
    string? role = null,
    // Rótulo cosmético opcional (ex.: "Secretária", "Gerente"), sem efeito em permissão.
    string? cargo = null);

public record UpdateTeacherRequest(
    string name, string email, string? phone,
    bool? active = null,
    string? cpf = null,
    List<string>? specialties = null,
    List<Guid>? unitIds = null,
    string? cep = null, string? logradouro = null, string? numero = null,
    string? complemento = null, string? bairro = null, string? cidade = null,
    string? estado = null,
    string? role = null,
    string? cargo = null);