using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Purchases;

public class PurchaseListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                package = new { id = "4fa85f64-5717-4562-b3fc-2c963f66afa6", name = "10 Aulas", credits = 10, price = 340.00 },
                amount = 340.00,
                coupon = (object?)null,
                paymentMethod = "pix",
                status = "confirmed",
                createdAt = "2026-06-06T10:00:00Z"
            }
        }
    };
}