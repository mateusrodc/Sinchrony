using MediatR;
using Sinchrony.Application.Attendance;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Commands.UpdateAttendance;

public record UpdateAttendanceCommand(
    Guid ClassId, Guid StudentId, string Status, Guid? ConfirmedById = null)
    : IRequest<UpdateAttendanceResultDto>;

public record UpdateAttendanceResultDto(IReadOnlyList<string> Warnings);

public class UpdateAttendanceCommandHandler(
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    AttendanceChangeService attendanceChangeService)
    : IRequestHandler<UpdateAttendanceCommand, UpdateAttendanceResultDto>
{
    public async Task<UpdateAttendanceResultDto> Handle(UpdateAttendanceCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var booking = await bookingRepository.GetByClassAndStudentAsync(
            request.ClassId, request.StudentId, ct)
            ?? throw DomainException.NotFound("No booking found for this student in this class.");

        if (booking.Status == BookingStatus.waitlisted)
            throw DomainException.Validation("INVALID_BOOKING_STATUS",
                "O aluno está na lista de espera e não pode ter presença lançada.");

        var result = await attendanceChangeService.ApplyAsync(
            @class, [new AttendanceChangeItem(booking, request.Status)],
            request.ConfirmedById, AttendanceChangeService.SourceSingle, ct);

        return new UpdateAttendanceResultDto(result.Warnings);
    }
}
