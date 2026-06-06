using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ErpClassListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "Yoga Matinal",
                type = "yoga",
                classTypeId = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
                instructor = "Ana Lima",
                teacherId = "2fa85f64-5717-4562-b3fc-2c963f66afa6",
                studioId = "4fa85f64-5717-4562-b3fc-2c963f66afa6",
                studioName = "Palmas",
                date = "2026-06-10",
                startTime = "07:00",
                endTime = "07:45",
                duration = 45,
                totalSpots = 20,
                availableSpots = 8,
                status = "scheduled",
                enrolledCount = 12
            }
        }
    };
}