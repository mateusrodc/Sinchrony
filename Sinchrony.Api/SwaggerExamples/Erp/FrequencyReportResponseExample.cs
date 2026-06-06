using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class FrequencyReportResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new { day = "Dom", count = 12 },
            new { day = "Seg", count = 45 },
            new { day = "Ter", count = 52 },
            new { day = "Qua", count = 38 },
            new { day = "Qui", count = 49 },
            new { day = "Sex", count = 55 },
            new { day = "Sáb", count = 30 }
        }
    };
}