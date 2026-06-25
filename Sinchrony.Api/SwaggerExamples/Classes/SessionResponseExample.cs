using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Classes
{
    public class SessionResponseExample : IExamplesProvider<object>
    {
        public object GetExamples() => new
        {
            id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            classId = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
            status = "in_progress",
            startedAt = "2026-06-10T06:30:00Z",
            duration = 45,
            enrolledCount = 18,
            attendedCount = 12,
            endedAt = (string?)null
        };
    }
}
