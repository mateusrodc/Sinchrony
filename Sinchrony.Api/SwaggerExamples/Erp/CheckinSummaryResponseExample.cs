using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class CheckinSummaryResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        confirmed = 3,
        attended = 2,
        noShow = 0
    };
}