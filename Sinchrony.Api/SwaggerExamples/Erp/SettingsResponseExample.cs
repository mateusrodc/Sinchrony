using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class SettingsResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        studioName = "4Sinchrony Experience",
        studioEmail = "contato@studio.com",
        studioPhone = "(63) 99999-0000",
        studioAddress = "Rua das Flores, 123 - Palmas/TO",
        bookingWindowDays = 7,
        cancellationDeadlineHours = 2,
        maxBookingsPerStudent = 5,
        allowWaitlist = true,
        autoConfirmBookings = true,
        sendBookingConfirmationEmail = true,
        sendReminderEmail = true,
        reminderHoursBefore = 24
    };
}