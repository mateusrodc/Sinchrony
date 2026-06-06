using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/bookings")]
public class ErpBookingsController(IBookingRepository bookingRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? classId, [FromQuery] Guid? studentId,
        [FromQuery] string? status, CancellationToken ct)
    {
        var bookings = await bookingRepository.ListErpAsync(classId, studentId, status, ct);
        return Ok(new
        {
            data = bookings.Select(b => new {
                id = b.Id,
                classId = b.ClassId,
                className = b.Class?.Name,
                studentId = b.StudentId,
                studentName = b.Student?.Name,
                studentEmail = b.Student?.Email,
                status = b.Status.ToString(),
                bikeNumber = b.BikeNumber,
                bookedAt = b.BookedAt,
                checkedIn = b.CheckedIn
            })
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Booking not found.");
        return Ok(new
        {
            id = booking.Id,
            classId = booking.ClassId,
            studentId = booking.StudentId,
            status = booking.Status.ToString(),
            bikeNumber = booking.BikeNumber,
            bookedAt = booking.BookedAt,
            checkedIn = booking.CheckedIn
        });
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        if (booking.Status == Domain.Enums.BookingStatus.attended)
            throw DomainException.Validation("ALREADY_ATTENDED", "Cannot cancel attended booking.");

        booking.Cancel();
        await bookingRepository.SaveAsync(ct);
        return Ok(new { id = booking.Id, status = booking.Status.ToString() });
    }

    [HttpPatch("{id}/no-show")]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        if (booking.Status != Domain.Enums.BookingStatus.confirmed)
            throw DomainException.Validation("INVALID_STATUS", "Only confirmed bookings can be marked as no-show.");

        booking.MarkNoShow();
        await bookingRepository.SaveAsync(ct);
        return Ok(new { id = booking.Id, status = booking.Status.ToString() });
    }
}