using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ReportSummaryResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        totalStudents = 248,
        activeStudents = 198,
        totalClasses = 184,
        totalBookings = 1240,
        occupancyRate = 76.5,
        checkinRate = 82.3,
        revenue = 45800.00,
        period = "Jul/2026"
    };
}