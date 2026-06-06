using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Teachers;

public class TeacherClassListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "Velo Power",
                type = "bike",
                instructor = "Ádria Silva",
                date = "2026-06-10",
                startTime = "06:30",
                endTime = "07:15",
                duration = 45,
                totalSpots = 20,
                availableSpots = 12,
                enrolledCount = 8,
                status = "scheduled",
                studio = new { id = "4fa85f64-5717-4562-b3fc-2c963f66afa6", name = "Palmas", address = "Rua das Flores, 123" }
            }
        }
    };
}