using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.Extensions;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Classes.Commands.ActivateClass;
using Sinchrony.Application.Classes.Commands.CreateClass;
using Sinchrony.Application.Classes.Commands.UpdateClass;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Net.NetworkInformation;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "teacher,admin")]
[ApiController]
[Route("api/classes")]
[Produces("application/json")]
public class ErpClassesController(
    IMediator mediator,
    IClassRepository classRepository,
    IUnitContext unitContext,
    IAuditService auditService) : ControllerBase
{
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpClassListResponseExample))]
    public async Task<IActionResult> List(
    [FromQuery] string? date, [FromQuery] string? from, [FromQuery] string? to,
    [FromQuery] string? type, [FromQuery] Guid? studioId, [FromQuery] string? status,
    CancellationToken ct)
    {
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        var (fromDate, toDate) = DateParams.ParseRange(from, to);
        var classes = await classRepository.ListAsync(parsedDate, type, studioId, ct, fromDate, toDate);
        // Admin de unidade vê só aulas dos studios da sua unidade
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId.HasValue)
        {
            var unitId = unitContext.UnitId.Value;
            classes = classes.Where(c => c.Studio != null && c.Studio.UnitId == unitId);
        }

        if (!string.IsNullOrEmpty(status))
            classes = classes.Where(c => c.Status.ToString() == status);
        return Ok(new { data = classes.Select(MapErpClass) });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpClassListResponseExample))]
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
        var created = await mediator.Send(new CreateClassCommand(
            AdminId, req.name, req.classTypeId, req.teacherId, req.studioId,
            req.date, req.startTime, req.endTime, req.duration, req.totalSpots), ct);

        return StatusCode(201, ListClassesQueryHandler.MapToDto(created));
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest req, CancellationToken ct)
    {
        var updated = await mediator.Send(new UpdateClassCommand(
            AdminId, id, req.name, req.classTypeId, req.teacherId, req.studioId,
            req.date, req.startTime, req.endTime, req.duration, req.totalSpots, req.status), ct);

        return Ok(ListClassesQueryHandler.MapToDto(updated));
    }

    // "Desativar" aula = status cancelled (o backend só aceita reserva/fila em aula scheduled).
    // Só é permitido sem reservas ativas; com reservas o admin cancela as reservas antes.
    [Authorize(Roles = "admin")]
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (!CanManage(@class)) return Forbid();

        @class.Deactivate();
        await classRepository.SaveAsync(ct);

        await auditService.LogAsync("class.status_changed", "Class", @class.Id, AdminId,
            $"From: {ClassStatus.scheduled} To: {ClassStatus.cancelled}", ct: ct);

        return Ok(new { data = new { id = @class.Id, name = @class.Name, active = false, status = @class.Status.ToString() } });
    }

    [Authorize(Roles = "admin")]
    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var @class = await mediator.Send(new ActivateClassCommand(AdminId, id), ct);

        return Ok(new { data = new { id = @class.Id, name = @class.Name, active = true, status = @class.Status.ToString() } });
    }

    // Falha fechado: admin que não é global precisa ter unidade e a aula precisa ser do studio dela.
    private bool CanManage(Class @class)
        => unitContext.IsGlobalAdmin
           || (unitContext.UnitId.HasValue && @class.Studio?.UnitId == unitContext.UnitId);

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
            enrolledCount = enrolled,
            seriesId = c.SeriesId,
            isException = c.IsException
        };
    }
}

public record CreateClassRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string date, string startTime, string? endTime,
    int duration, int totalSpots,
    string status = "scheduled"); // campo aceito conforme API_REFERENCE; endTime é ignorado (a API calcula)

public record UpdateClassRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string date, string startTime, string? endTime, int duration, int totalSpots, string status);

