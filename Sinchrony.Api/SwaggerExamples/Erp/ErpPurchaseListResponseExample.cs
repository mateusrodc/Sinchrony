using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ErpPurchaseListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                studentId = "5fa85f64-5717-4562-b3fc-2c963f66afa6",
                studentName = "Maria Silva",
                packageId = "4fa85f64-5717-4562-b3fc-2c963f66afa6",
                packageName = "Essence 10 aulas",
                amount = 350.00,
                paymentMethod = "pix",
                status = "confirmed",
                transactionId = "pay_abc123",
                createdAt = "2026-09-18T10:00:00Z"
            }
        },
        pagination = new { page = 1, pageSize = 20, total = 42, totalPages = 3 },
        summary = new { count = 42, totalAmount = 12450.00, confirmedAmount = 11800.00 }
    };
}
