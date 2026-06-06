using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Classes;

public class ClassStudentsResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
                name = "Carlos Silva",
                email = "carlos@email.com",
                bikeNumber = 7,
                status = "confirmed"
            },
            new
            {
                id = "b6d20202-6bb1-58b1-bc4e-7290fdfdd3aa",
                name = "Ana Lima",
                email = "ana@email.com",
                bikeNumber = 3,
                status = "attended"
            }
        }
    };
}