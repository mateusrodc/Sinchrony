using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ErpReportsController(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IPurchaseRepository purchaseRepository,
    IAttendanceRepository attendanceRepository,
    IStudioRepository studioRepository,
    IUnitContext unitContext) : ControllerBase
{
    // Mesmo padrão do ErpDashboardController: admin restrito a unidade só enxerga dados dos
    // studios daquela unidade. null = sem restrição (admin global).
    private async Task<HashSet<Guid>?> GetUnitStudioIdsAsync(CancellationToken ct)
    {
        if (unitContext.IsGlobalAdmin || !unitContext.UnitId.HasValue)
            return null;

        var studios = await studioRepository.ListAsync(ct);
        return studios.Where(s => s.UnitId == unitContext.UnitId.Value).Select(s => s.Id).ToHashSet();
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ReportSummaryResponseExample))]
    public async Task<IActionResult> Summary(
        [FromQuery] string? period,
        [FromQuery] Guid? teacherId,
        [FromQuery] Guid? classTypeId,
        [FromQuery] Guid? studioId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var unitStudioIds = await GetUnitStudioIdsAsync(ct);
        var firstOfMonth = new DateOnly(now.Year, now.Month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

        var students = unitStudioIds is null
            ? (await userRepository.ListStudentsAsync(null, ct)).ToList()
            : (await userRepository.ListStudentsByUnitAsync(unitContext.UnitId!.Value, ct)).ToList();

        var monthClasses = (await classRepository.ListForReportsAsync(
            firstOfMonth, lastOfMonth, studioId, teacherId, classTypeId, unitStudioIds, ct)).ToList();

        // Reservas confirmadas do mês — já vêm carregadas via Class.Bookings (Include), sem query extra
        var monthBookingsCount = monthClasses
            .SelectMany(c => c.Bookings)
            .Count(b => b.Status == BookingStatus.confirmed);

        var revenue = await purchaseRepository.TotalRevenueThisMonthAsync(ct);

        // Attendance restrito por período + unidade no banco; o cruzamento com monthClassIds
        // (já filtrado por teacherId/classTypeId/studioId acima) propaga esses filtros também.
        var monthAttendance = await attendanceRepository.ListForReportsAsync(firstOfMonth, lastOfMonth, unitStudioIds, ct);
        var monthClassIds = monthClasses.Select(c => c.Id).ToHashSet();
        var monthAttended = monthAttendance.Count(a =>
            monthClassIds.Contains(a.ClassId) &&
            a.Status == BookingStatus.attended &&
            a.Booking != null &&
            a.Booking.Status == BookingStatus.confirmed);

        var totalSpots = monthClasses.Sum(c => c.TotalSpots);
        var occupancy = totalSpots > 0
            ? Math.Round((double)monthBookingsCount * 100 / totalSpots, 1)
            : 0;

        var checkinRate = monthBookingsCount > 0
            ? Math.Min(100, Math.Round((double)monthAttended * 100 / monthBookingsCount, 1))
            : 0;

        return Ok(new
        {
            totalStudents = students.Count,
            activeStudents = students.Count(s => s.Status == StudentStatus.active),
            totalClasses = monthClasses.Count,
            totalBookings = monthBookingsCount,
            occupancyRate = occupancy,
            checkinRate,
            revenue,
            period = period ?? $"{now:MMM/yyyy}"
        });
    }

    [HttpGet("occupancy")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(OccupancyReportResponseExample))]
    public async Task<IActionResult> Occupancy(
        [FromQuery] int days = 30,
        [FromQuery] Guid? teacherId = null,
        [FromQuery] Guid? classTypeId = null,
        [FromQuery] Guid? studioId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var unitStudioIds = await GetUnitStudioIdsAsync(ct);
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

        var (classes, total) = await classRepository.ListForReportsPagedAsync(
            from, null, studioId, teacherId, classTypeId, unitStudioIds, page, pageSize, ct);
        var classList = classes.ToList();
        var classIds = classList.Select(c => c.Id).ToHashSet();

        var attendance = (await attendanceRepository.ListForReportsAsync(from, null, unitStudioIds, ct))
            .Where(a => classIds.Contains(a.ClassId))
            .ToList();

        var data = classList.Select(c =>
        {
            var booked = c.Bookings.Count(b => b.Status == BookingStatus.confirmed);
            var attended = attendance.Count(a =>
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
        });

        var paged = PagedResult.Create(data, page, pageSize, total);
        return Ok(new { paged.Data, paged.Pagination, days, from = from.ToString("yyyy-MM-dd") });
    }

    [HttpGet("frequency")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(FrequencyReportResponseExample))]
    public async Task<IActionResult> Frequency([FromQuery] int? days, CancellationToken ct)
    {
        var unitStudioIds = await GetUnitStudioIdsAsync(ct);
        // days ausente preserva o comportamento de hoje: sem filtro de período nenhum.
        var from = days.HasValue ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days.Value)) : (DateOnly?)null;

        var allAttendance = (await attendanceRepository.ListForReportsAsync(from, null, unitStudioIds, ct)).ToList();
        var dayNames = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        // Frequência por dia da semana baseada em attendance confirmado
        var frequency = Enumerable.Range(0, 7).Select(i =>
        {
            var count = allAttendance.Count(a =>
                a.Class != null &&
                (int)a.Class.Date.DayOfWeek == i &&
                a.Status == BookingStatus.attended);

            return new { day = dayNames[i], dayIndex = i, count };
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
            topStudents,
            days
        });
    }
}
