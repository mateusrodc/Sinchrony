using MediatR;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest;

public class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IEmailService emailService,
    ISettingsRepository settingsRepository,
    IAppSettings appSettings,
    ILogger<ForgotPasswordCommandHandler> logger) : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);
        if (user is null)
        {
            logger.LogInformation("ForgotPassword: email not found {Email}", request.Email);
            return;
        }

        var resetToken = PasswordResetToken.Create(user.Id, expiryMinutes: 60);
        await tokenRepository.AddAsync(resetToken, ct);
        await tokenRepository.SaveAsync(ct);

        logger.LogInformation("ForgotPassword: token created for {Email}", request.Email);

        // Busca settings ANTES do Task.Run — enquanto o contexto ainda está vivo
        var settings = await settingsRepository.GetAsync(ct);

        var resetLink = $"{appSettings.ErpUrl}/reset-password?token={resetToken.Token}";
        var userName = user.Name;
        var emailTo = user.Email;

        var body = $"""
            <h2>Redefinição de Senha — 4Sinchrony</h2>
            <p>Olá, {userName}!</p>
            <p>Recebemos uma solicitação para redefinir sua senha.</p>
            <p><a href="{resetLink}" style="background:#6366f1;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;">Redefinir Senha</a></p>
            <p>O link expira em <strong>1 hora</strong>.</p>
            <p>Se você não solicitou isso, ignore este email.</p>
            <br>
            <small>4Sinchrony Experience</small>
            """;

        // Dispara em background — passa settings já carregadas, não acessa mais o DbContext
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendWithSettingsAsync(
                    emailTo, "Redefinição de Senha — 4Sinchrony", body, settings,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background email sending failed for {Email}", emailTo);
            }
        });
    }
}