using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Teachers;
using Sinchrony.Application.Classes.Queries.ListClasses;
using Sinchrony.Domain.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize(Roles = "teacher,admin")]
[ApiController]
[Route("teachers/me")]
public class TeachersController(IMediator mediator, IClassRepository classRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet("classes")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(TeacherClassListResponseExample))]
    public async Task<IActionResult> MyClasses([FromQuery] string? date, CancellationToken ct)
    {
        DateOnly? parsedDate = DateOnly.TryParse(date, out var d) ? d : null;
        var classes = await classRepository.ListByTeacherAsync(UserId, parsedDate, ct);
        return Ok(new { data = classes.Select(ListClassesQueryHandler.MapToDto) });
    }

    [HttpGet("classes/today")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(TeacherClassListResponseExample))]
    public async Task<IActionResult> MyClassesToday(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var classes = await classRepository.ListByTeacherAsync(UserId, today, ct);
        return Ok(new { data = classes.Select(ListClassesQueryHandler.MapToDto) });
    }

    [HttpGet("classes/{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(TeacherClassListResponseExample))]
    public async Task<IActionResult> MyClass(Guid id, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(id, ct);
        if (@class is null || @class.TeacherId != UserId)
            return NotFound(new { error = new { code = "NOT_FOUND", message = "Class not found." } });

        return Ok(ListClassesQueryHandler.MapToDto(@class));
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(TeacherMetricsResponseExample))]
    public async Task<IActionResult> Metrics([FromQuery] int? month, [FromQuery] int? year, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var m = month ?? now.Month;
        var y = year ?? now.Year;

        var allClasses = await classRepository.ListByTeacherAsync(UserId, null, ct);
        var monthClasses = allClasses.Where(c => c.Date.Month == m && c.Date.Year == y).ToList();

        var totalAttended = monthClasses.Sum(c => c.Bookings.Count(b => b.Status == Domain.Enums.BookingStatus.attended));
        var uniqueStudents = monthClasses.SelectMany(c => c.Bookings.Select(b => b.StudentId)).Distinct().Count();
        var avgOccupancy = monthClasses.Any()
            ? monthClasses.Average(c =>
                c.TotalSpots > 0
                    ? (double)c.Bookings.Count(b => b.Status != Domain.Enums.BookingStatus.cancelled) / c.TotalSpots * 100
                    : 0)
            : 0;

        return Ok(new
        {
            totalClassesThisMonth = monthClasses.Count,
            totalStudentsAttended = totalAttended,
            uniqueStudents,
            averageOccupancyRate = Math.Round(avgOccupancy, 1),
            averageCheckinRate = 0,
            classesByWeek = new List<object>(),
            occupancyTrend = new List<object>()
        });
    }
}