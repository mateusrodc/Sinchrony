using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Classes
{
    public class AttendanceSummaryResponseExample : IExamplesProvider<object>
    {
        public object GetExamples() => new
        {
            total = 18,
            attended = 14,
            noShow = 2,
            pending = 2
        };
    }
}
