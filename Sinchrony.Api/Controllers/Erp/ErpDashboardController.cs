using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Services;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin,teacher")]
[ApiController]
[Produces("application/json")]
public class ErpDashboardController(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IStudioRepository studioRepository,
    IBookingRepository bookingRepository,
    IPurchaseRepository purchaseRepository,
    IAttendanceRepository attendanceRepository,
    IUnitContext unitContext) : ControllerBase
{
    [HttpGet("admin/dashboard")]
    [HttpGet("api/dashboard")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(DashboardResponseExample))]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        IEnumerable<User> students;
        IEnumerable<User> teachers;

        if (unitContext.IsGlobalAdmin || !unitContext.UnitId.HasValue)
        {
            students = await userRepository.ListStudentsAsync(null, ct);
            teachers = await userRepository.ListTeachersAsync(null, ct);
        }
        else
        {
            students = await userRepository.ListStudentsByUnitAsync(unitContext.UnitId.Value, ct);
            teachers = await userRepository.ListTeachersByUnitAsync(unitContext.UnitId.Value, ct);
        }
        var studios = await studioRepository.ListAsync(ct);
        var classes = await classRepository.ListAsync(null, null, null, ct);
        var allPurchases = await purchaseRepository.ListAllAsync(ct);
        var allBookings = await bookingRepository.ListErpAsync(null, null, null, ct);

        // Aulas do mês
        var monthClasses = classes
            .Where(c => c.Date.Month == now.Month && c.Date.Year == now.Year)
            .ToList();

        // Compras confirmadas do mês
        var monthPurchases = allPurchases
            .Where(p => p.Status == "confirmed" &&
                p.CreatedAt.Month == now.Month &&
                p.CreatedAt.Year == now.Year)
            .ToList();

        // Receita do mês
        var revenueThisMonth = monthPurchases.Sum(p => p.Amount);

        // Taxa de ocupação
        var occupancyRate = 0.0;
        if (monthClasses.Count > 0)
        {
            var totalSpots = monthClasses.Sum(c => c.TotalSpots);
            if (totalSpots > 0)
            {
                var monthBookings = allBookings.Count(b =>
                    b.Class != null &&
                    b.Class.Date.Month == now.Month &&
                    b.Class.Date.Year == now.Year &&
                    b.Status == BookingStatus.confirmed);
                occupancyRate = Math.Round((double)monthBookings / totalSpots * 100, 1);
            }
        }

        // Alunos ativos
        var activeSubscriptions = students.Count(s => s.Status == StudentStatus.active);

        // Check-ins hoje
        var checkinsToday = allBookings.Count(b =>
            b.Class != null &&
            b.Class.Date == today &&
            b.CheckedIn);

        // Próximas aulas (hoje e futuras, até 5)
        var upcomingClasses = classes
            .Where(c => c.Date >= today && c.Status == ClassStatus.scheduled)
            .OrderBy(c => c.Date).ThenBy(c => c.StartTime)
            .Take(5)
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                date = c.Date.ToString("yyyy-MM-dd"),
                startTime = c.StartTime,
                instructor = c.Teacher?.Name ?? string.Empty,
                enrolledCount = allBookings.Count(b =>
                    b.ClassId == c.Id && b.Status == BookingStatus.confirmed)
            })
            .ToList();

        // Últimos check-ins (até 5)
        var recentCheckins = allBookings
            .Where(b => b.CheckedIn && b.Class != null)
            .OrderByDescending(b => b.BookedAt)
            .Take(5)
            .Select(b => new
            {
                studentName = b.Student?.Name ?? string.Empty,
                className = b.Class?.Name ?? string.Empty,
                date = b.Class?.Date.ToString("yyyy-MM-dd"),
                startTime = b.Class?.StartTime
            })
            .ToList();

        // Receita mensal — últimos 6 meses
        var monthlyRevenue = Enumerable.Range(0, 6)
            .Select(i =>
            {
                var month = now.AddMonths(-i);
                var revenue = allPurchases
                    .Where(p =>
                        p.Status == "confirmed" &&
                        p.CreatedAt.Month == month.Month &&
                        p.CreatedAt.Year == month.Year)
                    .Sum(p => p.Amount);

                return new
                {
                    month = month.ToString("MMM/yyyy"),
                    revenue
                };
            })
            .OrderBy(x => x.month)
            .ToList();

        // Atividades recentes — últimas 10 (reservas, cancelamentos, pagamentos)
        var recentActivities = new List<object>();

        var recentBookings = allBookings
            .OrderByDescending(b => b.BookedAt)
            .Take(10)
            .Select(b => new
            {
                type = b.Status == BookingStatus.cancelled ? "cancellation" : "booking",
                description = b.Status == BookingStatus.cancelled
                    ? $"{b.Student?.Name ?? "Aluno"} cancelou reserva em {b.Class?.Name ?? "aula"}"
                    : $"{b.Student?.Name ?? "Aluno"} reservou vaga em {b.Class?.Name ?? "aula"}",
                timestamp = b.BookedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });

        var recentPayments = allPurchases
            .Where(p => p.Status == "confirmed")
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new
            {
                type = "payment",
                description = $"Pagamento de R$ {p.Amount:F2} confirmado",
                timestamp = p.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });

        recentActivities.AddRange(recentBookings);
        recentActivities.AddRange(recentPayments);

        var sortedActivities = recentActivities
            .OrderByDescending(a => ((dynamic)a).timestamp)
            .Take(10)
            .ToList();

        return Ok(new
        {
            totalStudios = studios.Count(),
            totalTeachers = teachers.Count(),
            totalStudents = students.Count(),
            totalClassesThisMonth = monthClasses.Count,
            revenueThisMonth,
            activeSubscriptions,
            occupancyRate,
            checkinsToday,
            upcomingClasses,
            recentCheckins,
            recentActivities = sortedActivities,
            monthlyRevenue
        });
    }
}