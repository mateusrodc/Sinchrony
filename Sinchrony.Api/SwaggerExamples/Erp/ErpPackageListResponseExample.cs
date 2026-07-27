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
                name = "10 Aulas Premium",
                description = "O mais popular do estúdio",
                credits = 10,
                price = 340.00,
                pricePerCredit = 34.00,
                validityDays = 90,
                popular = true,
                active = true,
                displayOrder = 3,
                createdAt = "2026-01-01T00:00:00Z",
                updatedAt = "2026-07-01T00:00:00Z",
                packageTypeId = "fc8a0038-cd1f-4234-b8c8-5d81fde59577",
                packageTypeName = "Premium",
                isFamily = false,
                purchaseStrategy = "block",
                maxDependents = 0,
                creditsPerMember = (int?)null,
                maxFutureBookings = 5,
                maxBookingsPerDay = 1,
                maxBookingsPerWeek = (int?)null,
                maxBookingsPerMonth = (int?)null,
                cancellationDeadlineHours = 2,
                bookingWindowDays = 7,
                earlyAccessHours = 48,
                allowWaitlist = true,
                waitlistPriority = (int?)null,
                reschedulingAllowed = true,
                reschedulingDeadlineHours = 24,
                noShowCreditPenalty = true,
                maxNoShowsBeforeBlock = (int?)null,
                noShowBlockWindowDays = 30,
                benefits = new[]
                {
                    new { id = "98201f47-1886-4a09-827c-1c4e78527b9c", name = "Acesso VIP", description = "Acesso antecipado às aulas", icon = "star" }
                },
                unitId = "00000000-0000-0000-0000-000000000001",
                unitName = "4Sinchrony Experience"
            }
        }
    };
}