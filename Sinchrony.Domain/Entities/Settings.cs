namespace Sinchrony.Domain.Entities;

public class Settings
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string StudioName { get; set; } = "4Sinchrony Experience";
    public string StudioEmail { get; set; } = string.Empty;
    public string? StudioPhone { get; set; }
    public string? StudioAddress { get; set; }
    public int BookingWindowDays { get; set; } = 7;
    public int CancellationDeadlineHours { get; set; } = 2;
    public int MaxBookingsPerStudent { get; set; } = 5;
    public bool AllowWaitlist { get; set; } = true;
    public bool AutoConfirmBookings { get; set; } = true;
    public bool SendBookingConfirmationEmail { get; set; } = true;
    public bool SendReminderEmail { get; set; } = true;
    public int ReminderHoursBefore { get; set; } = 24;
}