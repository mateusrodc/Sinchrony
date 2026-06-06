using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Bookings.Commands.CancelBooking;

public record CancelBookingCommand(Guid StudentId, Guid BookingId) : IRequest;

public class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    IUserRepository userRepository,
    ICreditTransactionRepository creditTransactionRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelBookingCommand>
{
    public async Task Handle(CancelBookingCommand request, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var booking = await bookingRepository.GetByIdAsync(request.BookingId, ct)
                ?? throw DomainException.NotFound("Booking not found.");

            if (booking.StudentId != request.StudentId)
                throw DomainException.Forbidden("You can only cancel your own bookings.");

            if (booking.Status == Domain.Enums.BookingStatus.attended)
                throw DomainException.Validation("ALREADY_ATTENDED", "Cannot cancel an attended booking.");

            var wasConfirmed = booking.Status == Domain.Enums.BookingStatus.confirmed;
            booking.Cancel();

            if (wasConfirmed)
            {
                var user = await userRepository.GetByIdAsync(request.StudentId, ct)
                    ?? throw DomainException.NotFound("User not found.");

                user.AddCredits(1);

                var creditTx = CreditTransaction.Create(
                    user.Id, +1, user.Credits,
                    $"Refund for cancelled booking {request.BookingId}",
                    "refund", request.BookingId);
                await creditTransactionRepository.AddAsync(creditTx, ct);
                await userRepository.SaveAsync(ct);
                await creditTransactionRepository.SaveAsync(ct);
            }

            await bookingRepository.SaveAsync(ct);

            await auditService.LogAsync(
                "booking.cancelled", "Booking",
                request.BookingId, request.StudentId, ct: ct);

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}