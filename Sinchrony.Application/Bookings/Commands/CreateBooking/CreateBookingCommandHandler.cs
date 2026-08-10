using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandHandler(
    IUserRepository userRepository,
    IClassRepository classRepository,
    IBookingRepository bookingRepository,
    IAttendanceRepository attendanceRepository,
    ICreditTransactionRepository creditTransactionRepository,
    IStudentPackageRepository studentPackageRepository,
    ISettingsRepository settingsRepository,
    IDependentRepository dependentRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateBookingCommand, BookingDto>
{
    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Quem paga o crédito é sempre o CallerId (responsável ou o próprio aluno)
            var caller = await userRepository.GetByIdAsync(request.CallerId, ct)
                ?? throw DomainException.NotFound("User not found.");

            if (caller.Status == StudentStatus.blocked)
                throw DomainException.Forbidden("Account is blocked.");

            if (caller.Status == StudentStatus.inactive)
                throw DomainException.Forbidden("Account is inactive.");

            if (caller.Credits <= 0)
                throw DomainException.Validation("INSUFFICIENT_CREDITS", "Insufficient credits.");

            // Reserva em nome de dependente — valida vínculo e permissão
            var isBookingForDependent = request.StudentId != request.CallerId;
            if (isBookingForDependent)
            {
                var dependentUser = await userRepository.GetByIdAsync(request.StudentId, ct)
                    ?? throw DomainException.NotFound("Student not found.");

                if (!dependentUser.IsDependent || dependentUser.ResponsibleStudentId != request.CallerId)
                    throw DomainException.Forbidden("Você não tem permissão para reservar em nome deste aluno.");

                var dependent = await dependentRepository.GetByUserIdAsync(request.StudentId, ct);
                if (dependent is not null && !dependent.CanBook)
                    throw DomainException.Forbidden("Este dependente não tem permissão para reservar aulas.");
            }

            var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
                ?? throw DomainException.NotFound("Class not found.");

            if (@class.Status != ClassStatus.scheduled)
                throw DomainException.Conflict("CLASS_UNAVAILABLE", "Class is not available for booking.");

            // Verifica conflito para o StudentId (dependente ou o próprio)
            var alreadyBooked = await bookingRepository.HasActiveBookingAsync(
                request.StudentId, request.ClassId, ct);
            if (alreadyBooked)
                throw DomainException.Conflict("BOOKING_CONFLICT", "Student is already enrolled in this class.");

            var hasConflict = await bookingRepository.HasTimeConflictAsync(
                request.StudentId, @class.Date, @class.StartTime, @class.EndTime, request.ClassId, ct);
            if (hasConflict)
                throw DomainException.Conflict("TIME_CONFLICT", "Time conflict with another booking.");

            // Cascata de regras baseada no pacote do CALLER
            var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(request.CallerId, ct);
            var settings = await settingsRepository.GetAsync(ct);

            if (settings is not null)
            {
                var bookingWindowDays = PackageRuleResolver.GetBookingWindowDays(studentPackage, settings);
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (@class.Date > today.AddDays(bookingWindowDays))
                    throw DomainException.Validation("BOOKING_WINDOW_EXCEEDED",
                        $"Reservas só podem ser feitas com até {bookingWindowDays} dias de antecedência.");

                var maxFuture = PackageRuleResolver.GetMaxFutureBookings(studentPackage);
                if (maxFuture.HasValue)
                {
                    var futureCount = await bookingRepository.CountFutureBookingsAsync(request.CallerId, ct);
                    if (futureCount >= maxFuture.Value)
                        throw DomainException.Validation("BOOKING_LIMIT_EXCEEDED",
                            $"Limite de {maxFuture.Value} reservas futuras atingido.");
                }

                var maxPerDay = PackageRuleResolver.GetMaxBookingsPerDay(studentPackage);
                if (maxPerDay.HasValue)
                {
                    var dayCount = await bookingRepository.CountBookingsOnDateAsync(
                        request.CallerId, @class.Date, ct);
                    if (dayCount >= maxPerDay.Value)
                        throw DomainException.Validation("BOOKING_LIMIT_EXCEEDED",
                            $"Limite de {maxPerDay.Value} reservas por dia atingido.");
                }

                var maxPerWeek = PackageRuleResolver.GetMaxBookingsPerWeek(studentPackage);
                if (maxPerWeek.HasValue)
                {
                    var weekCount = await bookingRepository.CountBookingsInWeekAsync(
                        request.CallerId, @class.Date, ct);
                    if (weekCount >= maxPerWeek.Value)
                        throw DomainException.Validation("BOOKING_LIMIT_EXCEEDED",
                            $"Limite de {maxPerWeek.Value} reservas por semana atingido.");
                }

                var maxPerMonth = PackageRuleResolver.GetMaxBookingsPerMonth(studentPackage);
                if (maxPerMonth.HasValue)
                {
                    var monthCount = await bookingRepository.CountBookingsInMonthAsync(
                        request.CallerId, @class.Date.Month, @class.Date.Year, ct);
                    if (monthCount >= maxPerMonth.Value)
                        throw DomainException.Validation("BOOKING_LIMIT_EXCEEDED",
                            $"Limite de {maxPerMonth.Value} reservas por mês atingido.");
                }
            }

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

            // Debita crédito do CALLER
            caller.DeductCredits(1);

            var creditTx = CreditTransaction.Create(
                caller.Id, -1, caller.Credits,
                $"Booking for class {@class.Name} on {@class.Date}" +
                (isBookingForDependent ? $" (dependente {request.StudentId})" : ""),
                "booking", null);
            await creditTransactionRepository.AddAsync(creditTx, ct);

            // Cria booking para o StudentId (dependente ou o próprio)
            var booking = Booking.Create(request.ClassId, request.StudentId, request.BikeNumber);
            var attendance = AttendanceRecord.Create(booking.Id, request.ClassId, request.StudentId);

            await bookingRepository.AddAsync(booking, ct);
            await attendanceRepository.AddAsync(attendance, ct);

            await userRepository.SaveAsync(ct);
            await creditTransactionRepository.SaveAsync(ct);

            await auditService.LogAsync(
                "booking.created", "Booking",
                booking.Id, request.CallerId,
                $"Class: {request.ClassId}, Student: {request.StudentId}, Bike: {request.BikeNumber}",
                ct: ct);

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