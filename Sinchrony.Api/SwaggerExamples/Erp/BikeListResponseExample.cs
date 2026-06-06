using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class BikeListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new { id = "3fa85f64-5717-4562-b3fc-2c963f66afa6", studioId = "4fa85f64-5717-4562-b3fc-2c963f66afa6", number = 1, status = "available", lastMaintenance = "2026-05-01", notes = (string?)null },
            new { id = "4fa85f64-5717-4562-b3fc-2c963f66afa6", studioId = "4fa85f64-5717-4562-b3fc-2c963f66afa6", number = 2, status = "broken",    lastMaintenance = "2026-04-15", notes = "Pedal danificado" },
            new { id = "5fa85f64-5717-4562-b3fc-2c963f66afa6", studioId = "4fa85f64-5717-4562-b3fc-2c963f66afa6", number = 3, status = "available", lastMaintenance = (string?)null, notes = (string?)null }
        }
    };
}