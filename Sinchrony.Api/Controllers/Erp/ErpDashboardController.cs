using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Produces("application/json")]
public class ErpDashboardController(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IStudioRepository studioRepository,
    IBookingRepository bookingRepository) : ControllerBase
{
    [HttpGet("admin/dashboard")]
    [HttpGet("api/dashboard")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(DashboardResponseExample))]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var students = await userRepository.ListStudentsAsync(null, ct);
        var teachers = await userRepository.ListTeachersAsync(null, ct);
        var studios = await studioRepository.ListAsync(ct);
        var classes = await classRepository.ListAsync(null, null, null, ct);
        var monthClasses = classes
            .Where(c => c.Date.Month == now.Month && c.Date.Year == now.Year)
            .ToList();

        // Calcula occupancyRate real
        var occupancyRate = 0.0;
        if (monthClasses.Count > 0)
        {
            var totalSpots = monthClasses.Sum(c => c.TotalSpots);
            if (totalSpots > 0)
            {
                var bookings = await bookingRepository.ListErpAsync(null, null, null, ct);
                var monthBookings = bookings.Count(b =>
                    b.Class != null &&
                    b.Class.Date.Month == now.Month &&
                    b.Class.Date.Year == now.Year &&
                    b.Status == BookingStatus.confirmed);

                occupancyRate = Math.Round((double)monthBookings / totalSpots * 100, 1);
            }
        }

        return Ok(new
        {
            totalStudios = studios.Count(),
            totalTeachers = teachers.Count(),
            totalStudents = students.Count(),
            totalClassesThisMonth = monthClasses.Count,
            revenueThisMonth = 0,
            activeSubscriptions = students.Count(s => s.Status == StudentStatus.active),
            occupancyRate,
            recentActivities = new List<object>(),
            monthlyRevenue = new List<object>()
        });
    }
}