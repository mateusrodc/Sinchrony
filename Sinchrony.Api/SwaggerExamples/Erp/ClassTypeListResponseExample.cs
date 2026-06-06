using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ClassTypeListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new { id = "1fa85f64-5717-4562-b3fc-2c963f66afa6", name = "Bike",  active = true },
            new { id = "2fa85f64-5717-4562-b3fc-2c963f66afa6", name = "Yoga",  active = true },
            new { id = "3fa85f64-5717-4562-b3fc-2c963f66afa6", name = "Pilates", active = true }
        }
    };
}