using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class EmailService(
    ISettingsRepository settingsRepository,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Settings? settings = null;
        try
        {
            settings = await settingsRepository.GetAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email not sent: failed to load settings.");
            return;
        }

        await SendWithSettingsAsync(to, subject, body, settings, ct);
    }

    public async Task SendWithSettingsAsync(
        string to, string subject, string body,
        Settings? settings, CancellationToken ct = default)
    {
        if (settings is null || string.IsNullOrEmpty(settings.SmtpHost))
        {
            logger.LogWarning("Email not sent: SMTP not configured (SmtpHost is empty).");
            return;
        }

        logger.LogInformation("Sending email to {To} via {Host}:{Port}",
            to, settings.SmtpHost, settings.SmtpPort);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(
                string.IsNullOrEmpty(settings.SmtpFrom)
                    ? settings.SmtpUser
                    : settings.SmtpFrom));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            client.Timeout = 8000;

            var secureOption = settings.SmtpSecure
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secureOption, cts.Token);
            await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword, cts.Token);
            await client.SendAsync(message, cts.Token);
            await client.DisconnectAsync(true, cts.Token);

            logger.LogInformation("Email sent successfully to {To}: {Subject}", to, subject);
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Email to {To} timed out after 10s. Check SMTP: {Host}:{Port}",
                to, settings.SmtpHost, settings.SmtpPort);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To} via {Host}:{Port}",
                to, settings.SmtpHost, settings.SmtpPort);
        }
    }
}