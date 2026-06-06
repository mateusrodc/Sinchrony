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
        occupancyRate = 74,
        recentActivities = new object[] { },
        monthlyRevenue = new object[] { }
    };
}