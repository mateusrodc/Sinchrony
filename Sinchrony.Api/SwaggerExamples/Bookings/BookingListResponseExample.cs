using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Bookings;

public class BookingListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                classId = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
                @class = new
                {
                    id = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
                    name = "Velo Power",
                    type = "bike",
                    instructor = "Ádria Silva",
                    date = "2026-06-10",
                    startTime = "06:30",
                    endTime = "07:15",
                    duration = 45,
                    studio = new { id = "4fa85f64-5717-4562-b3fc-2c963f66afa6", name = "Palmas", address = "Rua das Flores, 123" }
                },
                bikeNumber = 5,
                status = "confirmed",
                bookedAt = "2026-06-06T10:00:00Z",
                checkedIn = false
            }
        }
    };
}