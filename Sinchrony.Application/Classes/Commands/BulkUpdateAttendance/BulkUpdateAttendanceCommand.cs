using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Commands.BulkUpdateAttendance;

public record AttendanceUpdate(Guid StudentId, string Status);
public record BulkUpdateAttendanceCommand(Guid ClassId, List<AttendanceUpdate> Updates, Guid? ConfirmedById = null)
    : IRequest<BulkAttendanceResultDto>;
public record BulkAttendanceResultDto(bool Success, int Updated, int Created);

public class BulkUpdateAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository) : IRequestHandler<BulkUpdateAttendanceCommand, BulkAttendanceResultDto>
{
    public async Task<BulkAttendanceResultDto> Handle(
        BulkUpdateAttendanceCommand request, CancellationToken ct)
    {
        _ = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var updated = 0;
        var created = 0;

        foreach (var update in request.Updates)
        {
            var booking = await bookingRepository.GetByClassAndStudentAsync(
                request.ClassId, update.StudentId, ct);

            if (booking is null) continue;

            var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);

            if (attendance is null)
            {
                attendance = AttendanceRecord.Create(booking.Id, request.ClassId, update.StudentId);
                await attendanceRepository.AddAsync(attendance, ct);
                created++;
            }
            else
            {
                updated++;
            }

            attendance.UpdateStatus(update.Status, request.ConfirmedById);
        }

        await attendanceRepository.SaveAsync(ct);
        return new BulkAttendanceResultDto(true, updated, created);
    }
}