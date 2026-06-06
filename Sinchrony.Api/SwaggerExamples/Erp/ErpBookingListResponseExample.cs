using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ErpBookingListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                classId = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
                className = "Velo Power",
                studentId = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
                studentName = "Carlos Silva",
                studentEmail = "carlos@email.com",
                status = "attended",
                bikeNumber = 3,
                bookedAt = "2026-06-06T10:00:00Z",
                checkedIn = true
            }
        }
    };
}