using MediatR;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Attendance.Queries.ListAttendance;

public record ListAttendanceQuery(Guid ClassId) : IRequest<IEnumerable<AttendanceDto>>;

public record AttendanceDto(
    Guid Id, Guid StudentId, string StudentName,
    string? StudentAvatar,
    string Status, int? BikeNumber,
    Guid? ConfirmedById, DateTime? ConfirmedAt,
    bool CheckedIn);

public class ListAttendanceQueryHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository) : IRequestHandler<ListAttendanceQuery, IEnumerable<AttendanceDto>>
{
    public async Task<IEnumerable<AttendanceDto>> Handle(ListAttendanceQuery request, CancellationToken ct)
    {
        // Busca todos os bookings confirmados da aula
        var bookings = await bookingRepository.ListByClassAsync(request.ClassId, ct);
        var confirmedBookings = bookings
            .Where(b => b.Status == BookingStatus.confirmed)
            .ToList();

        // Busca attendance existentes
        var attendanceRecords = await attendanceRepository.ListByClassAsync(request.ClassId, ct);
        var attendanceByBookingId = attendanceRecords
            .ToDictionary(a => a.BookingId, a => a);

        // Combina — retorna todos os alunos com reserva, com ou sem attendance
        return confirmedBookings.Select(b =>
        {
            attendanceByBookingId.TryGetValue(b.Id, out var attendance);
            return new AttendanceDto(
                attendance?.Id ?? Guid.Empty,
                b.StudentId,
                b.Student?.Name ?? string.Empty,
                b.Student?.Avatar,
                attendance?.Status.ToString() ?? "pending",
                b.BikeNumber,
                attendance?.ConfirmedById,
                attendance?.ConfirmedAt,
                b.CheckedIn);
        });
    }
}