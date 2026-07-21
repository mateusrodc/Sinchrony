using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Bookings.Commands.CancelBooking;

public record CancelBookingCommand(Guid StudentId, Guid BookingId) : IRequest;

public class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    IAttendanceRepository attendanceRepository,
    IUserRepository userRepository,
    IAuditService auditService) : IRequestHandler<CancelBookingCommand>
{
    public async Task Handle(CancelBookingCommand request, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        if (booking.StudentId != request.StudentId)
            throw DomainException.Forbidden("Not your booking.");

        if (booking.Status == BookingStatus.cancelled)
            throw DomainException.Conflict("ALREADY_CANCELLED", "Booking is already cancelled.");

        var user = await userRepository.GetByIdAsync(request.StudentId, ct)
            ?? throw DomainException.NotFound("User not found.");

        booking.Cancel();
        user.AddCredits(1); // Estorna o crédito

        // Sincroniza attendance → no_show quando booking é cancelado
        var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);
        if (attendance is not null)
            attendance.UpdateStatus("no_show");

        await bookingRepository.SaveAsync(ct);
        await userRepository.SaveAsync(ct);
        await attendanceRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "booking.cancelled", "Booking",
            booking.Id, request.StudentId, ct: ct);
    }
}