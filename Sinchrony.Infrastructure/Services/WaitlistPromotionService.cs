using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

// DEMANDA_LISTA_ESPERA_TOLERANCIA_NOSHOW_PACOTES_BACKEND.md, seção 8: a notificação da fila
// (Notify + montar/mandar e-mail) estava duplicada — completa só em CancelBookingCommandHandler,
// e faltando (ERP cancel/no-show) ou pela metade (ToleranceEnforcementService, sem e-mail) nos
// outros 3 lugares que liberam vaga. Esse serviço centraliza os 4 caminhos.
public class WaitlistPromotionService(
    IWaitlistRepository waitlistRepository,
    ISettingsRepository settingsRepository,
    IEmailService emailService,
    ILogger<WaitlistPromotionService> logger) : IWaitlistPromotionService
{
    public async Task PromoteNextAsync(Guid classId, string className, CancellationToken ct = default)
    {
        var nextInWaitlist = await waitlistRepository.GetNextWaitingAsync(classId, ct);
        if (nextInWaitlist is null)
            return;

        nextInWaitlist.Notify(); // marca como notified + seta ExpiresAt = Now + 5min
        await waitlistRepository.SaveAsync(ct);

        // Extrai valores antes do Task.Run — o e-mail é disparado em segundo plano (não trava
        // a resposta de quem chamou) e não pode depender do escopo de DI do caller, que pode
        // ser descartado antes do envio terminar (mesma técnica já validada em produção quando
        // essa lógica ainda vivia dentro de CancelBookingCommandHandler).
        var studentEmail = nextInWaitlist.Student?.Email;
        var studentName = nextInWaitlist.Student?.Name ?? "aluno(a)";
        var expiresAt = nextInWaitlist.ExpiresAt ?? DateTime.UtcNow.AddMinutes(5); // Notify() acabou de setar, nunca null aqui
        var notifiedStudentId = nextInWaitlist.StudentId;

        if (string.IsNullOrWhiteSpace(studentEmail))
        {
            logger.LogWarning(
                "Waitlist: aluno {StudentId} notificado pra aula {ClassId} sem e-mail cadastrado, notificação não enviada.",
                notifiedStudentId, classId);
            return;
        }

        var settings = await settingsRepository.GetAsync(ct);

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
}
