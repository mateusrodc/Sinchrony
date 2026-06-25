using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Classes;
using Sinchrony.Application.Attendance.Commands.BulkAttendance;
using Sinchrony.Application.Attendance.Commands.ConfirmAllAttendance;
using Sinchrony.Application.Attendance.Commands.UpdateAttendance;
using Sinchrony.Application.Attendance.Queries.AttendanceSummary;
using Sinchrony.Application.Attendance.Queries.ListAttendance;
using Sinchrony.Application.Classes.Queries.GetClass;
using Sinchrony.Application.Classes.Queries.GetClassBikes;
using Sinchrony.Application.Classes.Queries.GetClassStudents;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Application.Classes.Queries.ListClassesToday;
using Sinchrony.Application.Sessions.Commands.EndSession;
using Sinchrony.Application.Sessions.Commands.StartSession;
using Sinchrony.Application.Sessions.Queries.GetSession;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("classes")]
[Produces("application/json")]
public class ClassesController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassListResponseExample))]
    public async Task<IActionResult> List(
    [FromQuery] string? date,
    [FromQuery] string? type,
    [FromQuery] Guid? studioId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
    {
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        var result = await mediator.Send(
            new ListClassesQuery(parsedDate, type, studioId, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("today")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassListResponseExample))]
    public async Task<IActionResult> Today(CancellationToken ct)
    {
        var result = await mediator.Send(new ListClassesTodayQuery(), ct);
        return Ok(new { data = result });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassDetailResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetClassQuery(id), ct);
        return Ok(result);
    }

    [Authorize(Roles = "teacher,admin")]
    [HttpGet("{id}/students")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassStudentsResponseExample))]
    public async Task<IActionResult> Students(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetClassStudentsQuery(id), ct);
        return Ok(new { data = result });
    }

    [HttpGet("{id}/bikes")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassBikesResponseExample))]
    public async Task<IActionResult> Bikes(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetClassBikesQuery(id), ct);
        return Ok(new { data = result });
    }

    [Authorize(Roles = "teacher,admin")]
    [HttpPost("{id}/session/start")]
    public async Task<IActionResult> StartSession(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new StartSessionCommand(id, UserId), ct);
        return Ok(result);
    }

    [Authorize(Roles = "teacher,admin")]
    [HttpPost("{id}/session/end")]
    public async Task<IActionResult> EndSession(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new EndSessionCommand(id, UserId), ct);
        return Ok(result);
    }

    [Authorize(Roles = "teacher,admin")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(SessionResponseExample))]
    [HttpGet("{id}/session")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSessionQuery(id), ct);
        return Ok(result);
    }

    [Authorize(Roles = "teacher,admin")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassStudentsResponseExample))]
    [HttpGet("{id}/attendance")]
    public async Task<IActionResult> Attendance(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ListAttendanceQuery(id), ct);
        return Ok(new { data = result });
    }

    [Authorize(Roles = "teacher,admin")]
    [HttpPut("{id}/attendance")]
    public async Task<IActionResult> UpdateAttendance(Guid id, [FromBody] UpdateAttendanceRequest req, CancellationToken ct)
    {
        await mediator.Send(new UpdateAttendanceCommand(id, req.studentId, req.status), ct);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "teacher,admin")]
    [HttpPost("{id}/attendance/bulk")]
    public async Task<IActionResult> BulkAttendance(Guid id, [FromBody] BulkAttendanceRequest req, CancellationToken ct)
    {
        var updates = req.updates.Select(u => new BulkAttendanceUpdate(u.studentId, u.status)).ToList();
        await mediator.Send(new BulkAttendanceCommand(id, updates), ct);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "teacher,admin")]
    [HttpPost("{id}/attendance/confirm-all")]
    public async Task<IActionResult> ConfirmAll(Guid id, CancellationToken ct)
    {
        await mediator.Send(new ConfirmAllAttendanceCommand(id, UserId), ct);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "teacher,admin")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(AttendanceSummaryResponseExample))]
    [HttpGet("{id}/attendance/summary")]
    public async Task<IActionResult> AttendanceSummary(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new AttendanceSummaryQuery(id), ct);
        return Ok(result);
    }
}

public record UpdateAttendanceRequest(Guid studentId, string status);
public record BulkAttendanceRequest(List<BulkAttendanceItemRequest> updates);
public record BulkAttendanceItemRequest(Guid studentId, string status);