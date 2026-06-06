using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Referrals;

public class ReferralResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        code = "MATEU123",
        url = "https://studio.com/ref/MATEU123",
        totalReferrals = 3,
        totalCreditsEarned = 6
    };
}