using Microsoft.Extensions.Logging;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Subscriptions;

// Bloqueio + alerta de admin quando um pacote recorrente vence de vez (as retentativas de
// renovação se esgotaram). Único lugar que sabe fazer isso — usado pelo RecurringRenewalService.
public class SubscriptionOverdueService(
    IUserRepository userRepository,
    IStudentPackageRepository studentPackageRepository,
    IAdminAlertRepository adminAlertRepository,
    ISettingsRepository settingsRepository,
    IEmailService emailService,
    IAuditService auditService,
    ILogger<SubscriptionOverdueService> logger)
{
    // Marca overdue, bloqueia o aluno (se ainda não bloqueado) e dispara o alerta de admin. A
    // Purchase da tentativa que esgotou já foi criada por quem chama (RecurringRenewalService).
    public async Task BlockAndAlertAsync(
        User user, StudentPackage studentPackage, string transactionId, decimal amount,
        DateOnly? dueDate, CancellationToken ct)
    {
        studentPackage.MarkOverdue();
        await studentPackageRepository.SaveAsync(ct);

        if (user.Status != StudentStatus.blocked)
        {
            user.Block("payment_failed");
            await userRepository.SaveAsync(ct);

            await auditService.LogAsync(
                "subscription.blocked", "User", user.Id, user.Id,
                $"StudentPackageId: {studentPackage.Id}, PaymentId: {transactionId}",
                ct: ct);

            logger.LogInformation(
                "StudentPackage {Id}: user {UserId} blocked after going overdue.",
                studentPackage.Id, user.Id);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var studentSettings = await settingsRepository.GetAsync(ct);
                var body = $"""
                    <h2>Acesso suspenso</h2>
                    <p>Olá, {user.Name}!</p>
                    <p>Não conseguimos confirmar o pagamento da sua assinatura após várias tentativas e seu acesso ao 4Sinchrony foi suspenso.</p>
                    <p>Seus créditos continuam guardados — atualize a forma de pagamento no aplicativo para reativar o acesso automaticamente.</p>
                    <br>
                    <small>4Sinchrony Experience</small>
                    """;
                SendEmailFireAndForget(user.Email, "Acesso suspenso — pagamento não confirmado", body, studentSettings);
            }
        }
        else
        {
            logger.LogInformation(
                "StudentPackage {Id}: user {UserId} already blocked, skipping re-block.",
                studentPackage.Id, user.Id);
        }

        await RaiseAlertAsync(user, studentPackage, transactionId, amount, dueDate, ct);
    }

    // Idempotente por TransactionId — reprocessar o mesmo evento (webhook reenviado, ou a
    // reconciliação encontrando o mesmo ciclo vencido) não duplica o alerta nem o e-mail.
    private async Task RaiseAlertAsync(
        User user, StudentPackage studentPackage, string transactionId, decimal amount,
        DateOnly? dueDate, CancellationToken ct)
    {
        if (await adminAlertRepository.ExistsByTransactionIdAsync(transactionId, ct))
            return;

        var alert = AdminAlert.CreateSubscriptionOverdue(user.Id, studentPackage.Id, transactionId, amount);
        await adminAlertRepository.AddAsync(alert, ct);
        await adminAlertRepository.SaveAsync(ct);

        var admins = await userRepository.ListAdminsAsync(ct);
        var settings = await settingsRepository.GetAsync(ct);

        var recipients = admins
            .Select(a => a.Email)
            .Concat(!string.IsNullOrWhiteSpace(settings?.StudioEmail) ? [settings.StudioEmail] : Array.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var dueDateBrt = BrasiliaTime.Convert((dueDate ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var body = $"""
            <h2>Assinatura vencida</h2>
            <p><strong>Aluno:</strong> {user.Name}</p>
            <p><strong>Pacote:</strong> {studentPackage.Package?.Name ?? "—"}</p>
            <p><strong>Valor:</strong> R$ {amount:F2}</p>
            <p><strong>Vencimento:</strong> {dueDateBrt:dd/MM/yyyy}</p>
            <p><a href="https://adm.4sinchrony.com.br/admin/students/{user.Id}">Ver aluno no ERP</a></p>
            """;

        foreach (var email in recipients)
            SendEmailFireAndForget(email, $"Assinatura vencida — {user.Name}", body, settings);
    }

    private void SendEmailFireAndForget(string to, string subject, string body, Settings? settings)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendWithSettingsAsync(to, subject, body, settings, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Subscription overdue: falha ao enviar e-mail para {Email}.", to);
            }
        });
    }
}
