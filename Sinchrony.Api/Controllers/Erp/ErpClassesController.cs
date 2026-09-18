using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
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
    [FromQuery] string? date, [FromQuery] string? type, [FromQuery] Guid? studioId, [FromQuery] string? status,
    CancellationToken ct)
    {
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        var classes = await classRepository.ListAsync(parsedDate, type, studioId, ct);
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
        var date = DateOnly.Parse(req.date);
        var @class = Class.Create(req.name, req.classTypeId, req.teacherId,
            req.studioId, date, req.startTime, req.endTime, req.duration, req.totalSpots);

        await classRepository.AddAsync(@class, ct);
        await classRepository.SaveAsync(ct);

        var created = await classRepository.GetByIdAsync(@class.Id, ct);

        await auditService.LogAsync("class.created", "Class", @class.Id, AdminId, $"Name: {@class.Name}", ct: ct);

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
        var previousStatus = @class.Status;

        // O cancelamento por aqui segue a mesma regra do /deactivate: só sem reservas ativas.
        if (status == ClassStatus.cancelled && previousStatus != ClassStatus.cancelled)
            @class.EnsureNoActiveBookings();

        @class.Update(req.name, req.classTypeId, req.teacherId, req.studioId,
            date, req.startTime, req.endTime, req.duration, req.totalSpots, status);

        await classRepository.SaveAsync(ct);

        // Não existe endpoint dedicado de cancelamento de aula — é feito via este PUT com
        // status "cancelled". Registrado separadamente por ser a mudança mais impactante
        // (mexe em quem já reservou), o resto do update fica num log só mais genérico.
        await auditService.LogAsync(
            previousStatus != status ? "class.status_changed" : "class.updated",
            "Class", @class.Id, AdminId,
            previousStatus != status ? $"From: {previousStatus} To: {status}" : $"Name: {@class.Name}",
            ct: ct);

        return Ok(ListClassesQueryHandler.MapToDto(@class));
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
        var @class = await classRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class not found.");

        if (!CanManage(@class)) return Forbid();

        // Datas de aula são horário local (UTC-3); usar UTC puro bloquearia aulas de hoje à noite.
        @class.Reactivate(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)));
        await classRepository.SaveAsync(ct);

        await auditService.LogAsync("class.status_changed", "Class", @class.Id, AdminId,
            $"From: {ClassStatus.cancelled} To: {ClassStatus.scheduled}", ct: ct);

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
            enrolledCount = enrolled
        };
    }
}

public record CreateClassRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string date, string startTime, string endTime,
    int duration, int totalSpots,
    string status = "scheduled"); // campo aceito conforme API_REFERENCE

public record UpdateClassRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string date, string startTime, string endTime, int duration, int totalSpots, string status);

