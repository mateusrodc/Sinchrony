using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/settings")]
public class ErpSettingsController(ISettingsRepository settingsRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var settings = await settingsRepository.GetAsync(ct) ?? new Settings();
        return Ok(MapSettings(settings));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest req, CancellationToken ct)
    {
        var settings = await settingsRepository.GetAsync(ct);
        if (settings is null)
        {
            settings = new Settings();
            await settingsRepository.AddAsync(settings, ct);
        }

        if (req.studioName is not null) settings.StudioName = req.studioName;
        if (req.studioEmail is not null) settings.StudioEmail = req.studioEmail;
        if (req.studioPhone is not null) settings.StudioPhone = req.studioPhone;
        if (req.studioAddress is not null) settings.StudioAddress = req.studioAddress;
        if (req.bookingWindowDays.HasValue) settings.BookingWindowDays = req.bookingWindowDays.Value;
        if (req.cancellationDeadlineHours.HasValue) settings.CancellationDeadlineHours = req.cancellationDeadlineHours.Value;
        if (req.maxBookingsPerStudent.HasValue) settings.MaxBookingsPerStudent = req.maxBookingsPerStudent.Value;
        if (req.allowWaitlist.HasValue) settings.AllowWaitlist = req.allowWaitlist.Value;
        if (req.autoConfirmBookings.HasValue) settings.AutoConfirmBookings = req.autoConfirmBookings.Value;
        if (req.sendBookingConfirmationEmail.HasValue) settings.SendBookingConfirmationEmail = req.sendBookingConfirmationEmail.Value;
        if (req.sendReminderEmail.HasValue) settings.SendReminderEmail = req.sendReminderEmail.Value;
        if (req.reminderHoursBefore.HasValue) settings.ReminderHoursBefore = req.reminderHoursBefore.Value;

        await settingsRepository.SaveAsync(ct);
        return Ok(MapSettings(settings));
    }

    private static object MapSettings(Settings s) => new
    {
        studioName = s.StudioName,
        studioEmail = s.StudioEmail,
        studioPhone = s.StudioPhone,
        studioAddress = s.StudioAddress,
        bookingWindowDays = s.BookingWindowDays,
        cancellationDeadlineHours = s.CancellationDeadlineHours,
        maxBookingsPerStudent = s.MaxBookingsPerStudent,
        allowWaitlist = s.AllowWaitlist,
        autoConfirmBookings = s.AutoConfirmBookings,
        sendBookingConfirmationEmail = s.SendBookingConfirmationEmail,
        sendReminderEmail = s.SendReminderEmail,
        reminderHoursBefore = s.ReminderHoursBefore
    };
}

public record UpdateSettingsRequest(
    string? studioName, string? studioEmail, string? studioPhone, string? studioAddress,
    int? bookingWindowDays, int? cancellationDeadlineHours, int? maxBookingsPerStudent,
    bool? allowWaitlist, bool? autoConfirmBookings,
    bool? sendBookingConfirmationEmail, bool? sendReminderEmail, int? reminderHoursBefore);