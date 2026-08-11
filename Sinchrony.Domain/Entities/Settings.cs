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
    // Cláusula 10 do Termo. "manual" (padrão, comportamento já existente): equipe marca
    // presença/falta na tela de check-in, sem ação automática do sistema. "automatic":
    // ToleranceEnforcementService varre reservas confirmadas sem check-in após
    // ClassStart + ToleranceMinutes e marca falta sozinho.
    public int ToleranceMinutes { get; set; } = 10;
    public string ToleranceMode { get; set; } = "manual";
    public bool AutoConfirmBookings { get; set; } = true;
    public bool SendBookingConfirmationEmail { get; set; } = true;
    public bool SendReminderEmail { get; set; } = true;
    public int ReminderHoursBefore { get; set; } = 24;
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFrom { get; set; }
    public bool SmtpSecure { get; set; }
}