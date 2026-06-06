using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class OccupancyReportResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new { date = "2026-06-06", className = "Velo Power", totalSpots = 20, booked = 12, attended = 10, occupancyPercent = 60 },
            new { date = "2026-06-07", className = "Yoga Matinal", totalSpots = 15, booked = 15, attended = 14, occupancyPercent = 100 }
        }
    };
}