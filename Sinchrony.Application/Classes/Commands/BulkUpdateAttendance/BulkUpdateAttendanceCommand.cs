using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Classes.Commands.BulkUpdateAttendance;

public record AttendanceUpdate(Guid StudentId, string Status);
public record BulkUpdateAttendanceCommand(Guid ClassId, List<AttendanceUpdate> Updates, Guid? ConfirmedById = null)
    : IRequest<BulkAttendanceResultDto>;
public record BulkAttendanceResultDto(bool Success, int Updated, int Created);

public class BulkUpdateAttendanceCommandHandler(
    IAttendanceRepository attendanceRepository,
    IBookingRepository bookingRepository,
    IClassRepository classRepository,
    INoShowPenaltyService noShowPenaltyService,
    IWaitlistPromotionService waitlistPromotionService) : IRequestHandler<BulkUpdateAttendanceCommand, BulkAttendanceResultDto>
{
    public async Task<BulkAttendanceResultDto> Handle(
        BulkUpdateAttendanceCommand request, CancellationToken ct)
    {
        var @class = await classRepository.GetByIdAsync(request.ClassId, ct)
            ?? throw DomainException.NotFound("Class not found.");

        var updated = 0;
        var created = 0;
        var newlyNoShow = new List<Guid>();

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

            // Captura antes de UpdateStatus pra só devolver crédito na transição pra no_show,
            // nunca de novo se o registro já estava marcado como falta.
            var wasNoShow = attendance.Status == BookingStatus.no_show;
            attendance.UpdateStatus(update.Status, request.ConfirmedById);

            if (update.Status == "no_show" && !wasNoShow)
                newlyNoShow.Add(update.StudentId);
        }

        await attendanceRepository.SaveAsync(ct);

        foreach (var studentId in newlyNoShow)
        {
            await noShowPenaltyService.ApplyAsync(studentId, ct);
            // Bulk update é sempre pra uma aula só — mesma request.ClassId pra todo mundo.
            await waitlistPromotionService.PromoteNextAsync(request.ClassId, @class.Name, ct);
        }

        return new BulkAttendanceResultDto(true, updated, created);
    }
}