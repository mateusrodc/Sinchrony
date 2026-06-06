using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class StudioListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "4fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "Palmas",
                address = "Rua das Flores, 123 - Palmas/TO",
                phone = "(63) 99999-0000",
                email = "palmas@sinchrony.com",
                active = true,
                capacity = 20,
                openingTime = "06:00",
                closingTime = "22:00"
            }
        }
    };
}