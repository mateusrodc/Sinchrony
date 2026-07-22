using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin,teacher")]
[ApiController]
[Route("api/students")]
[Produces("application/json")]
public class ErpStudentsController(
    IUserRepository userRepository,
    IPasswordService passwordService,
    IUnitContext unitContext) : ControllerBase
{
    private static object MapStudent(User u) => new
    {
        id = u.Id,
        name = u.Name,
        email = u.Email,
        cpf = u.Cpf,
        phone = u.Phone,
        status = u.Status.ToString(),
        plan = u.PlanName,
        credits = u.Credits,
        avatar = u.Avatar,
        unitId = u.UnitId,
        registeredAt = u.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        lastVisit = (string?)null,
        totalClasses = 0,
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
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (unitContext.IsGlobalAdmin || !unitContext.UnitId.HasValue)
        {
            var (items, total) = await userRepository.ListStudentsPagedAsync(status, page, pageSize, ct);
            return Ok(PagedResult.Create(items.Select(MapStudent), page, pageSize, total));
        }
        else
        {
            var all = await userRepository.ListStudentsByUnitAsync(unitContext.UnitId.Value, ct);
            if (!string.IsNullOrEmpty(status))
                all = all.Where(u => u.Status.ToString() == status);
            var list = all.ToList();
            var total = list.Count;
            var items = list.Skip((page - 1) * pageSize).Take(pageSize);
            return Ok(PagedResult.Create(items.Select(MapStudent), page, pageSize, total));
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        return Ok(MapStudent(student));
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        return Ok(new { data = new List<object>() });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest req, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(req.email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        if (!string.IsNullOrEmpty(req.cpf) && !CpfValidator.IsValid(req.cpf))
            throw DomainException.Validation("INVALID_CPF", "CPF inválido.");

        var hash = passwordService.HashPassword(Guid.NewGuid().ToString());
        var student = Domain.Entities.User.Create(req.name, req.email, req.phone, hash, Role.student,
            string.IsNullOrEmpty(req.cpf) ? null : CpfValidator.Sanitize(req.cpf));

        if (!string.IsNullOrEmpty(req.plan))
            student.UpdatePlan(req.plan);

        student.UpdateAddress(req.cep, req.logradouro, req.numero,
            req.complemento, req.bairro, req.cidade, req.estado);

        // Vincula à unidade do admin ou à unidade informada
        var unitId = req.unitId ?? unitContext.UnitId;
        if (unitId.HasValue)
            student.SetUnit(unitId.Value);

        await userRepository.AddAsync(student, ct);
        await userRepository.SaveAsync(ct);
        return StatusCode(201, MapStudent(student));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest req, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue
            && student.UnitId != unitContext.UnitId)
            return Forbid();

        student.UpdateProfile(req.name, req.email, req.phone, student.Avatar);

        if (!string.IsNullOrEmpty(req.cpf))
        {
            if (!CpfValidator.IsValid(req.cpf))
                throw DomainException.Validation("INVALID_CPF", "CPF inválido.");
            student.UpdateCpf(req.cpf);
        }

        if (!string.IsNullOrEmpty(req.status))
        {
            switch (req.status)
            {
                case "active": student.Reactivate(); break;
                case "inactive": student.Deactivate(); break;
                case "blocked": student.Block(); break;
            }
        }

        if (req.plan is not null) student.UpdatePlan(req.plan);

        student.UpdateAddress(req.cep, req.logradouro, req.numero,
            req.complemento, req.bairro, req.cidade, req.estado);

        if (req.unitId.HasValue)
            student.SetUnit(req.unitId.Value);

        await userRepository.SaveAsync(ct);
        return Ok(MapStudent(student));
    }
}

public record CreateStudentRequest(
    string name, string email, string? phone,
    string? plan, string? status, string? cpf,
    string? cep, string? logradouro, string? numero,
    string? complemento, string? bairro, string? cidade, string? estado,
    Guid? unitId = null);

public record UpdateStudentRequest(
    string name, string email, string? phone,
    string? status, string? plan, string? cpf,
    string? cep, string? logradouro, string? numero,
    string? complemento, string? bairro, string? cidade, string? estado,
    Guid? unitId = null);