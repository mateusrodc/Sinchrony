using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Classes;

public class ClassBikesResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new { number = 1, status = "occupied" },
            new { number = 2, status = "available" },
            new { number = 3, status = "available" },
            new { number = 4, status = "broken" },
            new { number = 5, status = "available" }
        }
    };
}