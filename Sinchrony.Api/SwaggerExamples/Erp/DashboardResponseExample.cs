using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class DashboardResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        totalStudios = 2,
        totalTeachers = 5,
        totalStudents = 248,
        totalClassesThisMonth = 184,
        revenueThisMonth = 45800.00,
        activeSubscriptions = 198,
        occupancyRate = 74.5,
        checkinsToday = 12,
        year = 2026,
        upcomingClasses = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "Velo Power",
                date = "2026-07-27",
                startTime = "06:30",
                instructor = "Ádria Silva",
                enrolledCount = 18
            }
        },
        recentCheckins = new[]
        {
            new
            {
                studentName = "Carlos Silva",
                className = "Velo Power",
                date = "2026-07-27",
                startTime = "06:30"
            }
        },
        recentActivities = new[]
        {
            new { type = "booking", description = "Carlos Silva reservou vaga em Velo Power", timestamp = "2026-07-27T06:00:00Z" },
            new { type = "payment", description = "Pagamento de R$ 150.00 confirmado", timestamp = "2026-07-26T15:30:00Z" }
        },
        monthlyRevenue = new[]
        {
            new { month = "2026-01", revenue = 38000.00 },
            new { month = "2026-02", revenue = 41200.00 },
            new { month = "2026-03", revenue = 39800.00 },
            new { month = "2026-04", revenue = 43500.00 },
            new { month = "2026-05", revenue = 44100.00 },
            new { month = "2026-06", revenue = 45800.00 },
            new { month = "2026-07", revenue = 42300.00 },
            new { month = "2026-08", revenue = 0.00 },
            new { month = "2026-09", revenue = 0.00 },
            new { month = "2026-10", revenue = 0.00 },
            new { month = "2026-11", revenue = 0.00 },
            new { month = "2026-12", revenue = 0.00 }
        },
        availableYears = new[] { 2026, 2025 }
    };
}