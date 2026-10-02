using MediatR;
using Sinchrony.Application.Attendance;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Enums;

namespace Sinchrony.Application.Classes.Commands.ConfirmAllAttendance;

public record ConfirmAllAttendanceCommand(Guid ClassId, Guid ConfirmedById) : IRequest<ConfirmAllResultDto>;
public record ConfirmAllResultDto(bool Success, int Total, int Updated, int Created);

public class ConfirmAllAttendanceCommandHandler(
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    AttendanceChangeService attendanceChangeService) : IRequestHandler<ConfirmAllAttendanceCommand, ConfirmAllResultDto>
{
    public async Task<ConfirmAllResultDto> Handle(
        ConfirmAllAttendanceCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var bookings = await bookingRepository.ListByClassAsync(request.ClassId, ct);
        var confirmed = bookings
            .Where(b => b.Status == BookingStatus.confirmed)
            .Select(b => new AttendanceChangeItem(b, "attended"))
            .ToList();

        // Sem reservas pendentes não há o que lançar (nem o que recusar por horário).
        if (confirmed.Count == 0)
            return new ConfirmAllResultDto(true, 0, 0, 0);

        var result = await attendanceChangeService.ApplyAsync(
            @class, confirmed, request.ConfirmedById, AttendanceChangeService.SourceConfirmAll, ct);

        return new ConfirmAllResultDto(true, confirmed.Count, result.Updated, result.Created);
    }
}
