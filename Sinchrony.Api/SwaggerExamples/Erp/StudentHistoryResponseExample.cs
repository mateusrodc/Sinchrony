using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class StudentHistoryResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new { date = "2026-06-06", className = "Velo Power",   status = "attended"  },
            new { date = "2026-06-04", className = "Yoga Matinal", status = "attended"  },
            new { date = "2026-06-02", className = "Pilates",      status = "cancelled" }
        }
    };
}