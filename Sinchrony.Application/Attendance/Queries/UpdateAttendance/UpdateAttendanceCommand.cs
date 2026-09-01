using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Classes.Commands.UpdateAttendance;

public record UpdateAttendanceCommand(
    Guid ClassId, Guid StudentId, string Status, Guid? ConfirmedById = null)
    : IRequest;

public class UpdateAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    INoShowPenaltyService noShowPenaltyService,
    IWaitlistPromotionService waitlistPromotionService) : IRequestHandler<UpdateAttendanceCommand>
{
    public async Task Handle(UpdateAttendanceCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
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

        // Captura antes de UpdateStatus pra só devolver crédito na transição pra no_show,
        // nunca de novo se o registro já estava marcado como falta.
        var wasNoShow = attendance.Status == BookingStatus.no_show;
        attendance.UpdateStatus(request.Status, request.ConfirmedById);

        // Sincroniza Booking.CheckedIn com o status do attendance
        if (request.Status == "attended")
            booking.SetCheckedIn(true);
        else if (request.Status == "no_show" || request.Status == "pending")
            booking.SetCheckedIn(false);

        await attendanceRepository.SaveAsync(ct);
        await bookingRepository.SaveAsync(ct);

        if (request.Status == "no_show" && !wasNoShow)
        {
            await noShowPenaltyService.ApplyAsync(request.StudentId, ct);
            await waitlistPromotionService.PromoteNextAsync(request.ClassId, @class.Name, ct);
        }
    }
}