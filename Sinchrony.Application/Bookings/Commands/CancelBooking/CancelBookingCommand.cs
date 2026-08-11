using MediatR;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Bookings.Commands.CancelBooking;

public record CancelBookingCommand(Guid StudentId, Guid BookingId) : IRequest;

public class CancelBookingCommandHandler(
    IBookingRepository bookingRepository,
    IAttendanceRepository attendanceRepository,
    IUserRepository userRepository,
    IDependentRepository dependentRepository,
    IStudentPackageRepository studentPackageRepository,
    ISettingsRepository settingsRepository,
    IAuditService auditService,
    IWaitlistRepository waitlistRepository,
    IEmailService emailService,
    ILogger<CancelBookingCommandHandler> logger) : IRequestHandler<CancelBookingCommand>
{
    public async Task Handle(CancelBookingCommand request, CancellationToken ct)
    {
        var booking = await bookingRepository.GetByIdAsync(request.BookingId, ct)
            ?? throw DomainException.NotFound("Booking not found.");

        // Permite cancelar se: é o próprio aluno OU é o responsável de um dependente
        var isOwner = booking.StudentId == request.StudentId;
        var isResponsible = false;

        if (!isOwner)
        {
            var bookingStudent = await userRepository.GetByIdAsync(booking.StudentId, ct);
            if (bookingStudent is not null &&
                bookingStudent.IsDependent &&
                bookingStudent.ResponsibleStudentId == request.StudentId)
            {
                isResponsible = true;
            }
        }

        if (!isOwner && !isResponsible)
            throw DomainException.Forbidden("Not your booking.");

        if (booking.Status == BookingStatus.cancelled)
            throw DomainException.Conflict("ALREADY_CANCELLED", "Booking is already cancelled.");

        // Valida canCancel do dependente
        if (isResponsible)
        {
            var dependent = await dependentRepository.GetByUserIdAsync(booking.StudentId, ct);
            if (dependent is not null && !dependent.CanCancel)
                throw DomainException.Forbidden("Este dependente não tem permissão para cancelar reservas.");
        }

        // Valida deadline via cascata
        var studentPackage = await studentPackageRepository.GetActiveByStudentAsync(request.StudentId, ct);
        var settings = await settingsRepository.GetAsync(ct);

        if (settings is not null && booking.Class is not null)
        {
            var deadlineHours = PackageRuleResolver.GetCancellationDeadlineHours(studentPackage, settings);
            var classStart = booking.Class.Date.ToDateTime(TimeOnly.Parse(booking.Class.StartTime));

            if (DateTime.UtcNow > classStart.AddHours(-deadlineHours))
                throw DomainException.Validation("CANCELLATION_DEADLINE_EXCEEDED",
                    $"Cancelamento deve ser feito com no mínimo {deadlineHours}h de antecedência.");
        }

        // Estorna crédito para o responsável (ou o próprio aluno)
        var creditOwnerId = isResponsible ? request.StudentId : booking.StudentId;
        var creditOwner = await userRepository.GetByIdAsync(creditOwnerId, ct)
            ?? throw DomainException.NotFound("User not found.");

        booking.Cancel();
        creditOwner.AddCredits(1);

        // Sincroniza attendance → no_show
        var attendance = await attendanceRepository.GetByBookingAsync(booking.Id, ct);
        if (attendance is not null)
            attendance.UpdateStatus("cancelled");

        var nextInWaitlist = await waitlistRepository.GetNextWaitingAsync(booking.ClassId, ct);
        if (nextInWaitlist is not null)
        {
            nextInWaitlist.Notify(); // marca como notified + seta ExpiresAt = Now + 5min
            await waitlistRepository.SaveAsync(ct);

            // Extrai valores antes do Task.Run — depois que o handler retorna, o DbContext do
            // request pode ser descartado, então nada aqui pode depender dele. SMTP não está mais
            // bloqueado (migrado da Render pra VPS), então manda o e-mail de verdade.
            var studentEmail = nextInWaitlist.Student?.Email;
            var studentName = nextInWaitlist.Student?.Name ?? "aluno(a)";
            var className = booking.Class?.Name ?? "sua aula";
            var expiresAt = nextInWaitlist.ExpiresAt ?? DateTime.UtcNow.AddMinutes(5); // Notify() acabou de setar, nunca null aqui
            var notifiedStudentId = nextInWaitlist.StudentId;
            var classId = booking.ClassId;

            if (!string.IsNullOrWhiteSpace(studentEmail))
            {
                var body = $"""
                    <h2>Vaga disponível — {className}</h2>
                    <p>Olá, {studentName}!</p>
                    <p>Uma vaga abriu em <strong>{className}</strong> e você é o(a) próximo(a) da lista de espera.</p>
                    <p>Você tem até <strong>{expiresAt:HH:mm}</strong> (5 minutos) para confirmar pelo aplicativo, antes que a vaga seja oferecida ao próximo da fila.</p>
                    <br>
                    <small>4Sinchrony Experience</small>
                    """;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await emailService.SendWithSettingsAsync(
                            studentEmail, $"Vaga disponível — {className}", body, settings,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Waitlist: falha ao notificar aluno {StudentId} pra aula {ClassId}",
                            notifiedStudentId, classId);
                    }
                });
            }
            else
            {
                logger.LogWarning(
                    "Waitlist: aluno {StudentId} notificado pra aula {ClassId} sem e-mail cadastrado, notificação não enviada.",
                    notifiedStudentId, classId);
            }
        }

        await bookingRepository.SaveAsync(ct);
        await userRepository.SaveAsync(ct);
        await attendanceRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "booking.cancelled", "Booking",
            booking.Id, request.StudentId, ct: ct);
    }
}