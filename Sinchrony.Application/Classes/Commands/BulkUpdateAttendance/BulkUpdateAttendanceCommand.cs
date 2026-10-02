using MediatR;
using Sinchrony.Application.Attendance;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Commands.BulkUpdateAttendance;

public record AttendanceUpdate(Guid StudentId, string Status);
public record BulkUpdateAttendanceCommand(Guid ClassId, List<AttendanceUpdate> Updates, Guid? ConfirmedById = null)
    : IRequest<BulkAttendanceResultDto>;
public record BulkAttendanceResultDto(bool Success, int Updated, int Created, IReadOnlyList<string>? Warnings = null);

public class BulkUpdateAttendanceCommandHandler(
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    AttendanceChangeService attendanceChangeService) : IRequestHandler<BulkUpdateAttendanceCommand, BulkAttendanceResultDto>
{
    public async Task<BulkAttendanceResultDto> Handle(
        BulkUpdateAttendanceCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var items = new List<AttendanceChangeItem>();
        foreach (var update in request.Updates)
        {
            var booking = await bookingRepository.GetByClassAndStudentAsync(
                request.ClassId, update.StudentId, ct);

            if (booking is null || booking.Status == BookingStatus.waitlisted) continue;

            items.Add(new AttendanceChangeItem(booking, update.Status));
        }

        // O serviço valida o horário de todos antes de aplicar: ou o pedido inteiro passa, ou nada muda.
        var result = await attendanceChangeService.ApplyAsync(
            @class, items, request.ConfirmedById, AttendanceChangeService.SourceBulk, ct);

        return new BulkAttendanceResultDto(true, result.Updated, result.Created, result.Warnings);
    }
}
