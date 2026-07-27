using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class UnitDashboardResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        unitId = "00000000-0000-0000-0000-000000000001",
        unitName = "4Sinchrony Experience",
        totalStudents = 120,
        totalClassesThisMonth = 48,
        revenueThisMonth = 18500.00,
        occupancyRate = 72.5,
        activeStudents = 98
    };
}