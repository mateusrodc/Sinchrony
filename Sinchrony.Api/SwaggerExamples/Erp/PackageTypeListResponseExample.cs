using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class PackageTypeListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "fc8a0038-cd1f-4234-b8c8-5d81fde59577",
                name = "Premium",
                active = true,
                isFamily = false,
                rank = 1,
                defaultMaxFutureBookings = 5,
                defaultMaxBookingsPerDay = 1,
                defaultMaxBookingsPerWeek = (int?)null,
                defaultMaxBookingsPerMonth = (int?)null,
                defaultCancellationDeadlineHours = 2,
                defaultBookingWindowDays = 7,
                defaultEarlyAccessHours = 48,
                defaultAllowWaitlist = true,
                defaultReschedulingAllowed = true,
                defaultReschedulingDeadlineHours = 24,
                defaultNoShowCreditPenalty = true,
                defaultMaxNoShowsBeforeBlock = (int?)null
            }
        }
    };
}