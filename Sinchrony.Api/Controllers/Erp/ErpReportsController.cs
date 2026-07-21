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
    IPurchaseRepository purchaseRepository,
    IAttendanceRepository attendanceRepository) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ReportSummaryResponseExample))]
    public async Task<IActionResult> Summary([FromQuery] string? period, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var students = (await userRepository.ListStudentsAsync(null, ct)).ToList();
        var classes = (await classRepository.ListAsync(null, null, null, ct)).ToList();
        var monthClasses = classes
            .Where(c => c.Date.Month == now.Month && c.Date.Year == now.Year)
            .ToList();
        var bookings = (await bookingRepository.ListErpAsync(null, null, null, ct)).ToList();
        var revenue = await purchaseRepository.TotalRevenueThisMonthAsync(ct);

        // Reservas confirmadas do mês
        var monthBookings = bookings.Where(b =>
            b.Class != null &&
            b.Class.Date.Month == now.Month &&
            b.Class.Date.Year == now.Year &&
            b.Status == BookingStatus.confirmed).ToList();

        // Checkins confirmados via attendance
        var allAttendance = (await attendanceRepository.ListAllAsync(ct)).ToList();
        var monthAttended = allAttendance.Count(a =>
            a.Class != null &&
            a.Class.Date.Month == now.Month &&
            a.Class.Date.Year == now.Year &&
            a.Status == BookingStatus.attended &&
            a.Booking != null &&
            a.Booking.Status == BookingStatus.confirmed);

        var totalSpots = monthClasses.Sum(c => c.TotalSpots);
        var occupancy = totalSpots > 0
            ? Math.Round((double)monthBookings.Count * 100 / totalSpots, 1)
            : 0;

        var checkinRate = monthBookings.Count > 0
            ? Math.Min(100, Math.Round((double)monthAttended * 100 / monthBookings.Count, 1))
            : 0;

        return Ok(new
        {
            totalStudents = students.Count,
            activeStudents = students.Count(s => s.Status == StudentStatus.active),
            totalClasses = monthClasses.Count,
            totalBookings = monthBookings.Count,
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
        var classes = (await classRepository.ListAsync(null, null, null, ct))
            .Where(c => c.Date >= from)
            .ToList();

        var allAttendance = (await attendanceRepository.ListAllAsync(ct)).ToList();

        var data = classes.Select(c =>
        {
            var booked = c.Bookings.Count(b => b.Status == BookingStatus.confirmed);
            var attended = allAttendance.Count(a =>
                a.ClassId == c.Id && a.Status == BookingStatus.attended);

            return new
            {
                date = c.Date.ToString("yyyy-MM-dd"),
                className = c.Name,
                instructor = c.Teacher?.Name ?? string.Empty,
                studio = c.Studio?.Name ?? string.Empty,
                totalSpots = c.TotalSpots,
                booked,
                attended,
                occupancyPercent = c.TotalSpots > 0
                    ? Math.Round((double)booked * 100 / c.TotalSpots, 1)
                    : 0,
                checkinPercent = booked > 0
                    ? Math.Round((double)attended * 100 / booked, 1)
                    : 0
            };
        }).OrderByDescending(x => x.date);

        return Ok(new { data, days, from = from.ToString("yyyy-MM-dd") });
    }

    [HttpGet("frequency")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(FrequencyReportResponseExample))]
    public async Task<IActionResult> Frequency(CancellationToken ct)
    {
        var allAttendance = (await attendanceRepository.ListAllAsync(ct)).ToList();
        var days = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        // Frequência por dia da semana baseada em attendance confirmado
        var frequency = Enumerable.Range(0, 7).Select(i =>
        {
            var count = allAttendance.Count(a =>
                a.Class != null &&
                (int)a.Class.Date.DayOfWeek == i &&
                a.Status == BookingStatus.attended);

            return new { day = days[i], dayIndex = i, count };
        }).OrderBy(x => x.dayIndex);

        // Frequência por tipo de aula
        var byClassType = allAttendance
            .Where(a => a.Class?.ClassType != null && a.Status == BookingStatus.attended)
            .GroupBy(a => a.Class!.ClassType!.Name)
            .Select(g => new { classType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // Alunos mais frequentes (top 10)
        var topStudents = allAttendance
            .Where(a => a.Status == BookingStatus.attended && a.Student != null)
            .GroupBy(a => new { a.StudentId, a.Student!.Name })
            .Select(g => new { studentId = g.Key.StudentId, name = g.Key.Name, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToList();

        return Ok(new
        {
            byDayOfWeek = frequency,
            byClassType,
            topStudents
        });
    }
}