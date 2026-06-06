using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Teachers;

public class TeacherMetricsResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        totalClassesThisMonth = 18,
        totalStudentsAttended = 142,
        uniqueStudents = 34,
        averageOccupancyRate = 78.5,
        averageCheckinRate = 0,
        classesByWeek = new object[] { },
        occupancyTrend = new object[] { }
    };
}