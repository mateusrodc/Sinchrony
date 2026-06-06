using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Health;

public class HealthResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new { status = "ok", db = "up" };
}