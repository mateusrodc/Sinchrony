using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ErpReportsController(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IBookingRepository bookingRepository,
    IPurchaseRepository purchaseRepository) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ReportSummaryResponseExample))]
    public async Task<IActionResult> Summary([FromQuery] string? period, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var students = (await userRepository.ListStudentsAsync(null, ct)).ToList();
        var classes = (await classRepository.ListAsync(null, null, null, ct)).ToList();
        var monthClasses = classes.Where(c => c.Date.Month == now.Month && c.Date.Year == now.Year).ToList();
        var bookings = (await bookingRepository.ListErpAsync(null, null, null, ct)).ToList();
        var revenue = await purchaseRepository.TotalRevenueThisMonthAsync(ct);

        var totalBooked = bookings.Count(b => b.Status != Domain.Enums.BookingStatus.cancelled);
        var totalAttended = bookings.Count(b => b.Status == Domain.Enums.BookingStatus.attended);
        var occupancy = totalBooked > 0 && monthClasses.Sum(c => c.TotalSpots) > 0
            ? totalBooked * 100 / monthClasses.Sum(c => c.TotalSpots)
            : 0;
        var checkinRate = totalBooked > 0 ? totalAttended * 100 / totalBooked : 0;

        return Ok(new
        {
            totalStudents = students.Count,
            activeStudents = students.Count(s => s.Status == Domain.Enums.StudentStatus.active),
            totalClasses = monthClasses.Count,
            totalBookings = totalBooked,
            occupancyRate = occupancy,
            checkinRate,
            revenue,
            period = period ?? $"{now:MMM/yyyy}"
        });
    }

    [HttpGet("occupancy")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(OccupancyReportResponseExample))]
    public async Task<IActionResult> Occupancy([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        var classes = await classRepository.ListAsync(null, null, null, ct);
        var recent = classes.Where(c => c.Date >= from).ToList();

        var data = recent.Select(c =>
        {
            var booked = c.Bookings.Count(b => b.Status != BookingStatus.cancelled);
            var attended = c.Bookings.Count(b => b.Status == BookingStatus.attended);
            return new
            {
                date = c.Date.ToString("yyyy-MM-dd"),
                className = c.Name,
                totalSpots = c.TotalSpots,
                booked,
                attended,
                occupancyPercent = c.TotalSpots > 0 ? booked * 100 / c.TotalSpots : 0
            };
        });

        return Ok(new { data });
    }

    [HttpGet("frequency")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(FrequencyReportResponseExample))]
    public async Task<IActionResult> Frequency(CancellationToken ct)
    {
        var classes = await classRepository.ListAsync(null, null, null, ct);
        var days = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        var frequency = classes
            .GroupBy(c => (int)c.Date.DayOfWeek)
            .Select(g => new { day = days[g.Key], count = g.Sum(c => c.Bookings.Count(b => b.Status == BookingStatus.attended)) })
            .OrderBy(x => x.day);

        return Ok(new { data = frequency });
    }
}