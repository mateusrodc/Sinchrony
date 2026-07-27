using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class CheckinListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                bookingId = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
                classId = "2fa85f64-5717-4562-b3fc-2c963f66afa6",
                studentId = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
                studentName = "Carlos Silva",
                studentAvatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/a5c10101_1784827794.jpg",
                studentPhone = "63984745681",
                className = "Velo Power",
                date = "2026-07-27",
                time = "06:45",
                status = "attended",
                confirmedBy = "Ádria Silva",
                confirmedAt = "2026-07-27T06:45:00Z"
            }
        }
    };
}