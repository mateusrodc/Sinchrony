using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class StudentPackagesErpResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "1921e791-ae0a-4550-aff1-4e370e631cb1",
                packageId = "d558b61c-f62e-48f1-88f3-58032a16ab38",
                packageName = "10 Aulas Premium",
                packageType = "Premium",
                status = "active",
                purchasedAt = "2026-07-23T21:14:35Z",
                startDate = "2026-07-23T21:46:22Z",
                endDate = "2026-10-21T21:46:22Z",
                allocations = new[]
                {
                    new { dependentId = (string?)null, creditsRemaining = 10 }
                }
            }
        }
    };
}