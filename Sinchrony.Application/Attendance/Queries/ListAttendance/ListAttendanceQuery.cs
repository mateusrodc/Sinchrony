using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Attendance.Queries.ListAttendance;

public record ListAttendanceQuery(Guid ClassId) : IRequest<IEnumerable<AttendanceDto>>;

public record AttendanceDto(
    Guid Id, Guid StudentId, string StudentName,
    string Status, int? BikeNumber,
    Guid? ConfirmedById, DateTime? ConfirmedAt);

public class ListAttendanceQueryHandler(IAttendanceRepository attendanceRepository)
    : IRequestHandler<ListAttendanceQuery, IEnumerable<AttendanceDto>>
{
    public async Task<IEnumerable<AttendanceDto>> Handle(ListAttendanceQuery request, CancellationToken ct)
    {
        var records = await attendanceRepository.ListByClassAsync(request.ClassId, ct);
        return records.Select(r => new AttendanceDto(
            r.Id, r.StudentId, r.Student?.Name ?? string.Empty,
            r.Status.ToString(), r.Booking?.BikeNumber,
            r.ConfirmedById, r.ConfirmedAt));
    }
}