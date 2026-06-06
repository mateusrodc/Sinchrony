using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
public class ErpDashboardController(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IStudioRepository studioRepository) : ControllerBase
{
    [HttpGet("admin/dashboard")]
    [HttpGet("api/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var students = await userRepository.ListStudentsAsync(null, ct);
        var teachers = await userRepository.ListTeachersAsync(null, ct);
        var studios = await studioRepository.ListAsync(ct);
        var classes = await classRepository.ListAsync(null, null, null, ct);
        var monthClasses = classes.Where(c => c.Date.Month == now.Month && c.Date.Year == now.Year).ToList();

        return Ok(new
        {
            totalStudios = studios.Count(),
            totalTeachers = teachers.Count(),
            totalStudents = students.Count(),
            totalClassesThisMonth = monthClasses.Count,
            revenueThisMonth = 0,
            activeSubscriptions = students.Count(s => s.Status == StudentStatus.active),
            occupancyRate = 74,
            recentActivities = new List<object>(),
            monthlyRevenue = new List<object>()
        });
    }
}