using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Attendance.Queries.AttendanceSummary;

public record AttendanceSummaryQuery(Guid ClassId) : IRequest<AttendanceSummaryDto>;

public record AttendanceSummaryDto(int Total, int Attended, int NoShow, int Pending);

public class AttendanceSummaryQueryHandler(IAttendanceRepository attendanceRepository)
    : IRequestHandler<AttendanceSummaryQuery, AttendanceSummaryDto>
{
    public async Task<AttendanceSummaryDto> Handle(AttendanceSummaryQuery request, CancellationToken ct)
    {
        var records = await attendanceRepository.ListByClassAsync(request.ClassId, ct);
        var list = records.ToList();

        return new AttendanceSummaryDto(
            list.Count,
            list.Count(r => r.Status == BookingStatus.attended),
            list.Count(r => r.Status == BookingStatus.no_show),
            list.Count(r => r.Status == BookingStatus.confirmed));
    }
}