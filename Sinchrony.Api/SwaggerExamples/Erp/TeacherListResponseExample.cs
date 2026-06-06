using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class TeacherListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "2fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "Ádria Silva",
                email = "adria@sinchrony.com",
                phone = "(63) 98888-0000",
                active = true,
                specialties = new[] { "Bike", "Pilates" }
            }
        }
    };
}