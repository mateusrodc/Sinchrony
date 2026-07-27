using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class OccupancyReportResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        days = 30,
        from = "2026-06-27",
        data = new[]
        {
            new
            {
                date = "2026-07-27",
                className = "Velo Power",
                instructor = "Ádria Silva",
                studio = "Palmas Auditoria",
                totalSpots = 20,
                booked = 18,
                attended = 15,
                occupancyPercent = 90.0,
                checkinPercent = 83.3
            }
        }
    };
}