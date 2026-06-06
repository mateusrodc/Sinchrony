using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Attendance.Commands.BulkAttendance;

public record BulkAttendanceUpdate(Guid StudentId, string Status);
public record BulkAttendanceCommand(Guid ClassId, List<BulkAttendanceUpdate> Updates) : IRequest;

public class BulkAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<BulkAttendanceCommand>
{
    public async Task Handle(BulkAttendanceCommand request, CancellationToken ct)
    {
        foreach (var update in request.Updates)
        {
            var record = await attendanceRepository.GetByClassAndStudentAsync(request.ClassId, update.StudentId, ct);
            if (record is null) continue;

            switch (update.Status)
            {
                case "attended":
                    record.Confirm(record.StudentId);
                    var booking = await bookingRepository.GetByIdAsync(record.BookingId, ct);
                    booking?.MarkAttended();
                    await bookingRepository.SaveAsync(ct);
                    break;
                case "no_show":
                    record.MarkNoShow();
                    break;
                case "confirmed":
                    record.MarkConfirmed();
                    break;
            }
        }

        await attendanceRepository.SaveAsync(ct);
    }
}