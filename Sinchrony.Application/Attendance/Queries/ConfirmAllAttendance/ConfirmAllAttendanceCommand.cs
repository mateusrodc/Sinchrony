using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Attendance.Commands.ConfirmAllAttendance;

public record ConfirmAllAttendanceCommand(Guid ClassId, Guid ConfirmedById) : IRequest;

public class ConfirmAllAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<ConfirmAllAttendanceCommand>
{
    public async Task Handle(ConfirmAllAttendanceCommand request, CancellationToken ct)
    {
        var records = await attendanceRepository.ListByClassAsync(request.ClassId, ct);

        foreach (var record in records.Where(r => r.Status == Domain.Enums.BookingStatus.confirmed))
        {
            record.Confirm(request.ConfirmedById);
            var booking = await bookingRepository.GetByIdAsync(record.BookingId, ct);
            booking?.MarkAttended();
        }

        await attendanceRepository.SaveAsync(ct);
        await bookingRepository.SaveAsync(ct);
    }
}