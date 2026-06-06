using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "teacher,admin")]
[ApiController]
[Route("api/classes")]
public class ErpClassesController(IClassRepository classRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
    [FromQuery] string? date, [FromQuery] string? type, [FromQuery] Guid? studioId,
    CancellationToken ct)
    {
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        var classes = await classRepository.ListAsync(parsedDate, type, studioId, ct);
        return Ok(new { data = classes.Select(MapErpClass) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class not found.");
        return Ok(MapErpClass(@class));
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest req, CancellationToken ct)
    {
        var date = DateOnly.Parse(req.date);
        var @class = Class.Create(req.name, req.classTypeId, req.teacherId,
            req.studioId, date, req.startTime, req.endTime, req.duration, req.totalSpots);

        await classRepository.AddAsync(@class, ct);
        await classRepository.SaveAsync(ct);

        var created = await classRepository.GetByIdAsync(@class.Id, ct);
        return StatusCode(201, ListClassesQueryHandler.MapToDto(created!));
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest req, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var date = DateOnly.Parse(req.date);
        var status = Enum.Parse<ClassStatus>(req.status, ignoreCase: true);

        @class.Update(req.name, req.classTypeId, req.teacherId, req.studioId,
            date, req.startTime, req.endTime, req.duration, req.totalSpots, status);

        await classRepository.SaveAsync(ct);
        return Ok(ListClassesQueryHandler.MapToDto(@class));
    }

    private static object MapErpClass(Domain.Entities.Class c)
    {
        var enrolled = c.Bookings.Count(b => b.Status != Domain.Enums.BookingStatus.cancelled);
        return new
        {
            id = c.Id,
            name = c.Name,
            type = c.ClassType?.Name,
            classTypeId = c.ClassTypeId,
            instructor = c.Teacher?.Name,
            teacherId = c.TeacherId,
            studioId = c.StudioId,
            studioName = c.Studio?.Name,
            date = c.Date.ToString("yyyy-MM-dd"),
            startTime = c.StartTime,
            endTime = c.EndTime,
            duration = c.Duration,
            totalSpots = c.TotalSpots,
            availableSpots = c.TotalSpots - enrolled,
            status = c.Status.ToString(),
            enrolledCount = enrolled
        };
    }
}

public record CreateClassRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string date, string startTime, string endTime, int duration, int totalSpots);

public record UpdateClassRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string date, string startTime, string endTime, int duration, int totalSpots, string status);

