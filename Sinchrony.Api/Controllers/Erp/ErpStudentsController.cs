using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/students")]
[Produces("application/json")]
public class ErpStudentsController(
    IUserRepository userRepository,
    IBookingRepository bookingRepository,
    IPasswordService passwordService) : ControllerBase
{
    [HttpGet]
    [SwaggerResponseExample(200, typeof(StudentListResponseExample))]
    public async Task<IActionResult> List(
    [FromQuery] string? status,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
    {
        var (items, total) = await userRepository.ListStudentsPagedAsync(status, page, pageSize, ct);
        return Ok(PagedResult.Create(items.Select(MapStudent), page, pageSize, total));
    }

    [HttpGet("{id}")]
    [SwaggerResponseExample(200, typeof(StudentDetailResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct);
        if (student is null || student.Role != Role.student)
            throw DomainException.NotFound("Student not found.");
        return Ok(MapStudent(student));
    }

    [HttpGet("{id}/history")]
    [SwaggerResponseExample(200, typeof(StudentHistoryResponseExample))]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct);
        if (student is null || student.Role != Role.student)
            throw DomainException.NotFound("Student not found.");

        var bookings = await bookingRepository.ListByStudentAsync(id, null, true, ct);
        var data = bookings.Select(b => new
        {
            date = b.Class?.Date.ToString("yyyy-MM-dd"),
            className = b.Class?.Name,
            status = b.Status.ToString()
        });
        return Ok(new { data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest req, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(req.email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        var hash = passwordService.HashPassword(Guid.NewGuid().ToString());
        var student = Sinchrony.Domain.Entities.User.Create(req.name, req.email, req.phone, hash, Role.student);

        await userRepository.AddAsync(student, ct);
        await userRepository.SaveAsync(ct);
        return StatusCode(201, MapStudent(student));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest req, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");

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

        student.UpdateProfile(req.name, req.email, req.phone, student.Avatar);
        await userRepository.SaveAsync(ct);
        return Ok(MapStudent(student));
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");
        student.Deactivate();
        await userRepository.SaveAsync(ct);
        return Ok(MapStudent(student));
    }

    [HttpPatch("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var student = await userRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Student not found.");
        student.Reactivate();
        await userRepository.SaveAsync(ct);
        return Ok(MapStudent(student));
    }

    private static object MapStudent(User u) => new
    {
        id = u.Id,
        name = u.Name,
        email = u.Email,
        phone = u.Phone,
        status = u.Status.ToString(),
        plan = u.PlanName,
        credits = u.Credits,
        registeredAt = u.CreatedAt.ToString("yyyy-MM-dd"),
        lastVisit = (string?)null,
        totalClasses = 0
    };
}

public record CreateStudentRequest(string name, string email, string? phone, string? plan, string? status);
public record UpdateStudentRequest(string name, string email, string? phone, string? status, string? plan);