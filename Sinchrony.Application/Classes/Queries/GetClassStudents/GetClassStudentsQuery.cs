using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Queries.GetClassStudents;

public record GetClassStudentsQuery(Guid ClassId) : IRequest<IEnumerable<ClassStudentDto>>;

public record ClassStudentDto(
    Guid Id, string Name, string Email,
    string? Avatar,
    string? Phone,
    int? BikeNumber, string Status);

public class GetClassStudentsQueryHandler(
    IBookingRepository bookingRepository, IClassRepository classRepository,
    IAttendanceRepository attendanceRepository)
    : IRequestHandler<GetClassStudentsQuery, IEnumerable<ClassStudentDto>>
{
    public async Task<IEnumerable<ClassStudentDto>> Handle(GetClassStudentsQuery request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var bookings = await bookingRepository.ListErpAsync(request.ClassId, null, null, ct);

        // UpdateAttendanceCommand/ConfirmAllAttendanceCommand escrevem a presença em
        // AttendanceRecord.Status, não em Booking.Status — ler só b.Status fazia a tela sempre
        // voltar pra "pending" ao reabrir o app, mesmo com a presença já confirmada
        // (DEMANDA_CLASSES_STUDENTS_ATTENDANCE_DESSINCRONIZADO_BACKEND.md). Mesmo padrão de
        // leitura cruzada já usado em ListAttendanceQueryHandler.
        var attendanceByBooking = (await attendanceRepository.ListByClassAsync(request.ClassId, ct))
            .ToDictionary(a => a.BookingId);

        return bookings
            .Where(b => b.Status != Domain.Enums.BookingStatus.cancelled)
            .Select(b => new ClassStudentDto(
                b.Student!.Id,
                b.Student.Name,
                b.Student.Email,
                b.Student.Avatar,
                b.Student.Phone,
                b.BikeNumber,
                attendanceByBooking.TryGetValue(b.Id, out var att) ? att.Status.ToString() : b.Status.ToString()));
    }
}