using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandHandler(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IBookingRepository bookingRepository,
    IAttendanceRepository attendanceRepository,
    ICreditTransactionRepository creditTransactionRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var user = await userRepository.GetByIdAsync(request.StudentId, ct)
                ?? throw DomainException.NotFound("User not found.");

            if (user.Status == StudentStatus.blocked)
                throw DomainException.Forbidden("Account is blocked.");

            if (user.Credits <= 0)
                throw DomainException.Validation("INSUFFICIENT_CREDITS", "Insufficient credits.");

            var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
                ?? throw DomainException.NotFound("Class not found.");

            if (@class.Status != ClassStatus.scheduled)
                throw DomainException.Conflict("CLASS_UNAVAILABLE", "Class is not available for booking.");

            var alreadyBooked = await bookingRepository.HasActiveBookingAsync(
                request.StudentId, request.ClassId, ct);
            if (alreadyBooked)
                throw DomainException.Conflict("BOOKING_CONFLICT", "Student is already enrolled in this class.");

            var hasConflict = await bookingRepository.HasTimeConflictAsync(
                request.StudentId, @class.Date, @class.StartTime, @class.EndTime, request.ClassId, ct);
            if (hasConflict)
                throw DomainException.Conflict("TIME_CONFLICT", "Time conflict with another booking.");

            var activeCount = await classRepository.CountActiveBookingsWithLockAsync(request.ClassId, ct);
            if (activeCount >= @class.TotalSpots)
                throw DomainException.Conflict("CLASS_FULL", "Class is full.");

            if (request.BikeNumber.HasValue)
            {
                var bikeOccupied = await bookingRepository.IsBikeOccupiedAsync(
                    request.ClassId, request.BikeNumber.Value, ct);
                if (bikeOccupied)
                    throw DomainException.Conflict("BIKE_OCCUPIED", "Bike is already taken.");
            }

            user.DeductCredits(1);

            var creditTx = CreditTransaction.Create(
                user.Id, -1, user.Credits,
                $"Booking for class {@class.Name} on {@class.Date}",
                "booking", null);
            await creditTransactionRepository.AddAsync(creditTx, ct);

            var booking = Booking.Create(request.ClassId, request.StudentId, request.BikeNumber);
            var attendance = AttendanceRecord.Create(booking.Id, request.ClassId, request.StudentId);

            await bookingRepository.AddAsync(booking, ct);
            await attendanceRepository.AddAsync(attendance, ct); // <-- linha que faltava

            await userRepository.SaveAsync(ct);
            await creditTransactionRepository.SaveAsync(ct);

            await auditService.LogAsync(
                "booking.created", "Booking",
                booking.Id, request.StudentId,
                $"Class: {request.ClassId}, Bike: {request.BikeNumber}", ct: ct);

            await unitOfWork.CommitAsync(ct);

            return new BookingDto(booking.Id, booking.ClassId, booking.BikeNumber,
                booking.Status.ToString(), booking.BookedAt, booking.CheckedIn);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}