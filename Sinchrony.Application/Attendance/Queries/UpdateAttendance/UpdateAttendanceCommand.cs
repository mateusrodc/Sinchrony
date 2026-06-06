using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Attendance.Commands.UpdateAttendance;

public record UpdateAttendanceCommand(Guid ClassId, Guid StudentId, string Status) : IRequest;

public class UpdateAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository)
    : IRequestHandler<UpdateAttendanceCommand>
{
    public async Task Handle(UpdateAttendanceCommand request, CancellationToken ct)
    {
        var record = await attendanceRepository.GetByClassAndStudentAsync(request.ClassId, request.StudentId, ct)
            ?? throw DomainException.NotFound("Attendance record not found.");

        switch (request.Status)
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
            default:
                throw DomainException.Validation("INVALID_STATUS", "Invalid attendance status.");
        }

        await attendanceRepository.SaveAsync(ct);
    }
}