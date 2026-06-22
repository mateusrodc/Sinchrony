using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/teachers")]
[Produces("application/json")]
public class ErpTeachersController(
    IUserRepository userRepository,
    IPasswordService passwordService) : ControllerBase
{
    [HttpGet]
    [SwaggerResponseExample(200, typeof(TeacherListResponseExample))]
    public async Task<IActionResult> List([FromQuery] bool? active, CancellationToken ct)
    {
        var teachers = await userRepository.ListTeachersAsync(active, ct);
        return Ok(new { data = teachers.Select(MapTeacher) });
    }

    [HttpGet("{id}")]
    [SwaggerResponseExample(200, typeof(TeacherListResponseExample))]
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

        var hash = passwordService.HashPassword(req.password);
        var teacher = Sinchrony.Domain.Entities.User.Create(req.name, req.email, req.phone, hash, Role.teacher);

        await userRepository.AddAsync(teacher, ct);
        await userRepository.SaveAsync(ct);
        return StatusCode(201, MapTeacher(teacher));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeacherRequest req, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct)
        ?? throw DomainException.NotFound("Teacher not found.");

        teacher.UpdateProfile(req.name, req.email, req.phone, teacher.Avatar);

        if (req.active == false) teacher.Deactivate();
        else if (req.active == true) teacher.Reactivate();

        await userRepository.SaveAsync(ct);
        return Ok(MapTeacher(teacher));
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Teacher not found.");
        teacher.Deactivate();
        await userRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Teacher not found.");
        teacher.Reactivate();
        await userRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id}/send-password")]
    public async Task<IActionResult> SendPassword(Guid id, CancellationToken ct)
    {
        var teacher = await userRepository.GetByIdAsync(id, ct);
        if (teacher is null || teacher.Role != Role.teacher)
            throw DomainException.NotFound("Teacher not found.");

        var tempPassword = Guid.NewGuid().ToString("N")[..10];
        var hash = passwordService.HashPassword(tempPassword);
        teacher.ChangePassword(hash);

        foreach (var token in teacher.RefreshTokens.Where(t => !t.Revoked))
            token.Revoke();

        await userRepository.SaveAsync(ct);
        return Ok(new { success = true, message = "Senha temporária gerada.", temporaryPassword = tempPassword });
    }

    private static object MapTeacher(User u) => new
    {
        id = u.Id,
        name = u.Name,
        email = u.Email,
        phone = u.Phone,
        active = u.Active,
        specialties = new List<string>()
    };
}

public record CreateTeacherRequest(
    string name, string email, string? phone,
    string password, bool active,
    List<string>? specialties = null); // aceita mas ignora por ora

public record UpdateTeacherRequest(
    string name, string email, string? phone,
    bool? active = null,
    List<string>? specialties = null); // aceita mas ignora por ora