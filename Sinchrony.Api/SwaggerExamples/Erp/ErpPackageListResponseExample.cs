using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ErpPackageListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "10 Aulas",
                description = "O mais popular",
                credits = 10,
                price = 340.00,
                pricePerCredit = 34.00,
                validityDays = 90,
                popular = true,
                active = true,
                displayOrder = 3,
                createdAt = "2026-01-01T00:00:00Z",
                updatedAt = "2026-01-01T00:00:00Z"
            }
        }
    };
}