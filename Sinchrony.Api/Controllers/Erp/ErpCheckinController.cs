using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "teacher,admin")]
[ApiController]
[Route("api/checkin")]
[Produces("application/json")]
public class ErpCheckinController(IAttendanceRepository attendanceRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [SwaggerResponseExample(200, typeof(CheckinListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var all = await attendanceRepository.ListAllAsync(ct);
        return Ok(new { data = all.Select(MapCheckin) });
    }

    [HttpGet("class/{classId}")]
    [SwaggerResponseExample(200, typeof(CheckinListResponseExample))]
    public async Task<IActionResult> ByClass(Guid classId, CancellationToken ct)
    {
        var records = await attendanceRepository.ListByClassAsync(classId, ct);
        return Ok(new { data = records.Select(MapCheckin) });
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var record = await attendanceRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Checkin record not found.");

        record.Confirm(UserId);
        await attendanceRepository.SaveAsync(ct);
        return Ok(MapCheckin(record));
    }

    [HttpGet("summary")]
    [SwaggerResponseExample(200, typeof(CheckinSummaryResponseExample))]
    public async Task<IActionResult> Summary([FromQuery] Guid classId, CancellationToken ct)
    {
        var records = (await attendanceRepository.ListByClassAsync(classId, ct)).ToList();
        return Ok(new
        {
            confirmed = records.Count(r => r.Status == Domain.Enums.BookingStatus.confirmed),
            attended = records.Count(r => r.Status == Domain.Enums.BookingStatus.attended),
            noShow = records.Count(r => r.Status == Domain.Enums.BookingStatus.no_show)
        });
    }

    private static object MapCheckin(Domain.Entities.AttendanceRecord r) => new
    {
        id = r.Id,
        bookingId = r.BookingId,
        classId = r.ClassId,
        studentId = r.StudentId,
        studentName = r.Student?.Name,
        className = r.Class?.Name,
        date = r.CreatedAt.ToString("yyyy-MM-dd"),
        time = r.ConfirmedAt?.ToString("HH:mm"),
        status = r.Status.ToString(),
        confirmedBy = r.ConfirmedBy?.Name,
        confirmedAt = r.ConfirmedAt
    };
}