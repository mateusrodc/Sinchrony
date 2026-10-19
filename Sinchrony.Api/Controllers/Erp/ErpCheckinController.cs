using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.Extensions;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Attendance;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "teacher,admin")]
[ApiController]
[Route("api/checkin")]
[Produces("application/json")]
public class ErpCheckinController(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    IUnitContext unitContext,
    AttendanceChangeService attendanceChangeService) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(CheckinListResponseExample))]
    public async Task<IActionResult> List(
        [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (fromDate, toDate) = DateParams.ParseRange(from, to);
        var all = await attendanceRepository.ListAllAsync(ct, fromDate, toDate);
        return Ok(new { data = all.Select(MapCheckin) });
    }

    [HttpGet("class/{classId}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(CheckinListResponseExample))]
    public async Task<IActionResult> ByClass(Guid classId, CancellationToken ct)
    {
        var records = await attendanceRepository.ListByClassAsync(classId, ct);
        return Ok(new { data = records.Select(MapCheckin) });
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        // Tenta por AttendanceRecord.Id primeiro, depois por BookingId
        var record = await attendanceRepository.GetByIdAsync(id, ct)
            ?? await attendanceRepository.GetByBookingAsync(id, ct)
            ?? throw DomainException.NotFound("Checkin record not found.");

        var @class = await classRepository.GetByIdAsync(record.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");
        var booking = await bookingRepository.GetByIdAsync(record.BookingId, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        await attendanceChangeService.ApplyAsync(
            @class, [new AttendanceChangeItem(booking, "attended")],
            UserId, AttendanceChangeService.SourceErpCheckin, ct);

        return Ok(MapCheckin(record));
    }

    // Contagem só dos registros que ainda valem: reserva não cancelada e ainda nesta aula
    // (registro de reserva cancelada ou remarcada não entra).
    [HttpGet("summary")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(CheckinSummaryResponseExample))]
    public async Task<IActionResult> Summary([FromQuery] Guid classId, CancellationToken ct)
    {
        var records = (await attendanceRepository.ListByClassAsync(classId, ct))
            .Where(r => r.Booking is { } b && b.Status != BookingStatus.cancelled && b.ClassId == classId)
            .ToList();
        return Ok(new
        {
            confirmed = records.Count(r => r.Status == BookingStatus.confirmed),
            attended = records.Count(r => r.Status == BookingStatus.attended),
            noShow = records.Count(r => r.Status == BookingStatus.no_show),
            cancelledWithinDeadline = (await bookingRepository.ListByClassAsync(classId, ct))
                .Count(b => b.Status == BookingStatus.cancelled)
        });
    }

    // Uma linha por aula do dia, com os totais contados a partir das reservas não canceladas —
    // a mesma regra para a lista e para os detalhes. Padrão: hoje (horário de Brasília).
    [HttpGet("classes")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> Classes([FromQuery] string? date, CancellationToken ct)
    {
        var day = DateOnly.TryParse(date, out var parsed) ? parsed : BrasiliaTime.ToDate(DateTime.UtcNow);
        var classes = await classRepository.ListAsync(day, null, null, ct);

        var scope = await unitContext.GetScopeAsync(ct);
        if (scope is not null)
            classes = classes.Where(c => c.Studio?.UnitId is { } unitId && scope.Contains(unitId));

        return Ok(new
        {
            data = classes.Select(c =>
            {
                var active = c.Bookings.Where(b => b.Status != BookingStatus.cancelled).ToList();
                return new
                {
                    classId = c.Id,
                    className = c.Name,
                    date = c.Date.ToString("yyyy-MM-dd"),
                    startTime = c.StartTime,
                    endTime = c.EndTime,
                    status = c.Status.ToString(),
                    teacherName = c.Teacher?.Name,
                    studioName = c.Studio?.Name,
                    totalSpots = c.TotalSpots,
                    booked = active.Count,
                    pending = active.Count(b => b.Status == BookingStatus.confirmed),
                    attended = active.Count(b => b.Status == BookingStatus.attended),
                    noShow = active.Count(b => b.Status == BookingStatus.no_show)
                };
            })
        });
    }

    private static object MapCheckin(Domain.Entities.AttendanceRecord r) => new
    {
        id = r.Id,
        bookingId = r.BookingId,
        classId = r.ClassId,
        studentId = r.StudentId,
        studentName = r.Student?.Name,
        studentAvatar = r.Student?.Avatar,
        studentPhone = r.Student?.Phone,
        className = r.Class?.Name,
        // Data da aula (não a da criação do registro), a mesma que a lista de reservas usa.
        date = r.Class is not null ? r.Class.Date.ToString("yyyy-MM-dd") : r.CreatedAt.ToString("yyyy-MM-dd"),
        time = r.ConfirmedAt?.ToString("HH:mm"),
        status = r.Status.ToString(),
        confirmedBy = r.ConfirmedBy?.Name,
        confirmedAt = r.ConfirmedAt
    };
}