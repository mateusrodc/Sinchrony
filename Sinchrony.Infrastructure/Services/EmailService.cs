using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class EmailService(
    ISettingsRepository settingsRepository,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var settings = await settingsRepository.GetAsync(ct);

        if (settings is null || string.IsNullOrEmpty(settings.SmtpHost))
        {
            logger.LogWarning("Email not sent: SMTP not configured.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(
                string.IsNullOrEmpty(settings.SmtpFrom) ? settings.SmtpUser : settings.SmtpFrom));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();

            var secureOption = settings.SmtpSecure
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secureOption, ct);
            await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", to);
        }
    }
}