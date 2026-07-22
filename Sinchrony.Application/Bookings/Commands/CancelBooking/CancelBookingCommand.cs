using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Bookings.Commands.CancelBooking;

public record CancelBookingCommand(Guid StudentId, Guid BookingId) : IRequest;

public class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    IAttendanceRepository attendanceRepository,
    IUserRepository userRepository,
    IStudentPackageRepository studentPackageRepository,
    ISettingsRepository settingsRepository,
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

        // Valida deadline via cascata
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(request.StudentId, ct);
        var settings = await settingsRepository.GetAsync(ct);

        if (settings is not null && booking.Class is not null)
        {
            var deadlineHours = PackageRuleResolver.GetCancellationDeadlineHours(studentPackage, settings);
            var classStart = booking.Class.Date.ToDateTime(TimeOnly.Parse(booking.Class.StartTime));

            if (DateTime.UtcNow > classStart.AddHours(-deadlineHours))
                throw DomainException.Validation("CANCELLATION_DEADLINE_EXCEEDED",
                    $"Cancelamento deve ser feito com no mínimo {deadlineHours}h de antecedência.");
        }

        var user = await userRepository.GetByIdAsync(request.StudentId, ct)
            ?? throw DomainException.NotFound("User not found.");

        booking.Cancel();
        user.AddCredits(1);

        // Sincroniza attendance → no_show
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