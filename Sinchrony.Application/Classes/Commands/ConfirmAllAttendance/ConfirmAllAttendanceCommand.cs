using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Enums;

namespace Sinchrony.Application.Classes.Commands.ConfirmAllAttendance;

public record ConfirmAllAttendanceCommand(Guid ClassId, Guid ConfirmedById) : IRequest<ConfirmAllResultDto>;
public record ConfirmAllResultDto(bool Success, int Total, int Updated, int Created);

public class ConfirmAllAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository) : IRequestHandler<ConfirmAllAttendanceCommand, ConfirmAllResultDto>
{
    public async Task<ConfirmAllResultDto> Handle(
        ConfirmAllAttendanceCommand request, CancellationToken ct)
    {
        _ = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var bookings = await bookingRepository.ListByClassAsync(request.ClassId, ct);
        var confirmed = bookings
            .Where(b => b.Status == BookingStatus.confirmed)
            .ToList();

        var updated = 0;
        var created = 0;

        foreach (var booking in confirmed)
        {
            var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);

            if (attendance is null)
            {
                attendance = AttendanceRecord.Create(booking.Id, request.ClassId, booking.StudentId);
                await attendanceRepository.AddAsync(attendance, ct);
                created++;
            }
            else
            {
                updated++;
            }

            attendance.UpdateStatus("attended", request.ConfirmedById);
        }

        await attendanceRepository.SaveAsync(ct);
        return new ConfirmAllResultDto(true, confirmed.Count, updated, created);
    }
}