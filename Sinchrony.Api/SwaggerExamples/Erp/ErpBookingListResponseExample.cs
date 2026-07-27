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
                studentAvatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/a5c10101_1784853105.jpg",
                studentPhone = "63984745681",
                status = "confirmed",
                bikeNumber = 3,
                bookedAt = "2026-07-27T10:00:00Z",
                checkedIn = true
            }
        },
        pagination = new { page = 1, pageSize = 20, total = 1, totalPages = 1 }
    };
}