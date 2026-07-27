using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Classes;

public class ClassDetailResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        name = "Velo Power",
        type = "Bike",
        usesBikes = true,
        classTypeId = "1fa85f64-5717-4562-b3fc-2c963f66afa6",
        instructor = "Ádria Silva",
        instructorAvatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/2fa85f64_1784827850.jpg",
        teacherId = "2fa85f64-5717-4562-b3fc-2c963f66afa6",
        date = "2026-07-27",
        startTime = "06:30",
        endTime = "07:15",
        duration = 45,
        totalSpots = 20,
        availableSpots = 12,
        enrolledCount = 8,
        status = "scheduled",
        studio = new
        {
            id = "4fa85f64-5717-4562-b3fc-2c963f66afa6",
            name = "Palmas Auditoria",
            address = "Rua das Flores, 123",
            capacity = 20,
            openingTime = "06:00",
            closingTime = "22:00",
            unitId = "00000000-0000-0000-0000-000000000001"
        },
        bikes = new[]
        {
            new { id = "b1a85f64-5717-4562-b3fc-2c963f66afa6", number = 1, isAvailable = true },
            new { id = "b2a85f64-5717-4562-b3fc-2c963f66afa6", number = 2, isAvailable = false },
            new { id = "b3a85f64-5717-4562-b3fc-2c963f66afa6", number = 3, isAvailable = true }
        }
    };
}