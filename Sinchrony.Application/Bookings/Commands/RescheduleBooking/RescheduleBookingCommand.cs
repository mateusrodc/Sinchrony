using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Bookings.Commands.RescheduleBooking;

public record RescheduleBookingCommand(Guid StudentId, Guid BookingId, Guid NewClassId) : IRequest;

public class RescheduleBookingCommandHandler(
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    IStudentPackageRepository studentPackageRepository,
    ISettingsRepository settingsRepository) : IRequestHandler<RescheduleBookingCommand>
{
    public async Task Handle(RescheduleBookingCommand request, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        if (booking.StudentId != request.StudentId)
            throw DomainException.Forbidden("Not your booking.");

        if (booking.Status == BookingStatus.cancelled)
            throw DomainException.Conflict("BOOKING_CANCELLED", "Cannot reschedule a cancelled booking.");

        var currentClass = await classRepository.GetByIdAsync(booking.ClassId, ct)
            ?? throw DomainException.NotFound("Current class not found.");

        var newClass = await classRepository.GetByIdAsync(request.NewClassId, ct)
            ?? throw DomainException.NotFound("New class not found.");

        // Resolve regras pela cascata
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(request.StudentId, ct);
        var settings = await settingsRepository.GetAsync(ct)
            ?? throw DomainException.NotFound("Settings not configured.");

        // Valida reschedulingAllowed
        if (!PackageRuleResolver.GetReschedulingAllowed(studentPackage))
            throw DomainException.Validation("RESCHEDULING_NOT_ALLOWED",
                "Remarcação não permitida para o seu plano.");

        // Valida deadline
        var deadlineHours = PackageRuleResolver.GetReschedulingDeadlineHours(studentPackage);
        var classStart = currentClass.Date.ToDateTime(TimeOnly.Parse(currentClass.StartTime));
        if (DateTime.UtcNow > classStart.AddHours(-deadlineHours))
            throw DomainException.Validation("RESCHEDULING_DEADLINE_EXCEEDED",
                $"Remarcação deve ser feita com no mínimo {deadlineHours}h de antecedência.");

        // Valida vaga na nova aula
        var spots = await classRepository.CountActiveBookingsWithLockAsync(request.NewClassId, ct);
        if (spots >= newClass.TotalSpots)
            throw DomainException.Conflict("CLASS_FULL", "A nova aula está lotada.");

        // Troca a aula — sem debitar/estornar crédito
        booking.Reschedule(request.NewClassId);
        await bookingRepository.SaveAsync(ct);
    }
}