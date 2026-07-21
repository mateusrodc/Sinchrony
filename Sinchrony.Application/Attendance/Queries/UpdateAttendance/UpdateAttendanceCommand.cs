using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Classes.Commands.UpdateAttendance;

public record UpdateAttendanceCommand(
    Guid ClassId, Guid StudentId, string Status, Guid? ConfirmedById = null)
    : IRequest;

public class UpdateAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository) : IRequestHandler<UpdateAttendanceCommand>
{
    public async Task Handle(UpdateAttendanceCommand request, CancellationToken ct)
    {
        _ = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var booking = await bookingRepository.GetByClassAndStudentAsync(
            request.ClassId, request.StudentId, ct)
            ?? throw DomainException.NotFound("No booking found for this student in this class.");

        var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);

        if (attendance is null)
        {
            attendance = AttendanceRecord.Create(booking.Id, request.ClassId, request.StudentId);
            await attendanceRepository.AddAsync(attendance, ct);
        }

        attendance.UpdateStatus(request.Status, request.ConfirmedById);

        // Sincroniza Booking.CheckedIn com o status do attendance
        if (request.Status == "attended")
            booking.SetCheckedIn(true);
        else if (request.Status == "no_show" || request.Status == "pending")
            booking.SetCheckedIn(false);

        await attendanceRepository.SaveAsync(ct);
        await bookingRepository.SaveAsync(ct);
    }
}