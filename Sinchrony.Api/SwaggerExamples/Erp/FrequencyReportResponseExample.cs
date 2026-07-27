using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class FrequencyReportResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        byDayOfWeek = new[]
        {
            new { day = "Dom", dayIndex = 0, count = 0 },
            new { day = "Seg", dayIndex = 1, count = 12 },
            new { day = "Ter", dayIndex = 2, count = 18 },
            new { day = "Qua", dayIndex = 3, count = 15 },
            new { day = "Qui", dayIndex = 4, count = 20 },
            new { day = "Sex", dayIndex = 5, count = 25 },
            new { day = "Sáb", dayIndex = 6, count = 8 }
        },
        byClassType = new[]
        {
            new { classType = "Bike", count = 45 },
            new { classType = "Yoga", count = 30 },
            new { classType = "Pilates", count = 23 }
        },
        topStudents = new[]
        {
            new { studentId = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99", name = "Carlos Silva", count = 18 },
            new { studentId = "b6c20202-6bb1-58b1-bc6e-3ce85f9e31b0", name = "Ana Souza", count = 14 }
        }
    };
}