using MediatR;
using Microsoft.Extensions.Configuration;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest;

public class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetRepository,
    IEmailService emailService,
    ISettingsRepository settingsRepository,
    IConfiguration configuration) : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);
        if (user is null) return; // não revela se o email existe

        var token = Guid.NewGuid().ToString("N");
        var expiry = DateTime.UtcNow.AddHours(1);

        var resetToken = PasswordResetToken.Create(user.Id, 60);
        await passwordResetRepository.AddAsync(resetToken, ct);
        await passwordResetRepository.SaveAsync(ct);

        var erpUrl = configuration["ErpUrl"]?.TrimEnd('/') ?? "https://app.4sinchrony.com.br";
        var resetUrl = $"{erpUrl}/reset-password?token={resetToken.Token}";

        var subject = "Redefinição de senha — 4Sinchrony";

        var body = $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;padding:40px 0;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);">
                    <tr>
                      <td style="background:#1a1a2e;padding:32px;text-align:center;">
                        <h1 style="color:#ffffff;margin:0;font-size:24px;letter-spacing:2px;">4SINCHRONY</h1>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:40px 48px;">
                        <h2 style="color:#1a1a2e;margin:0 0 16px;">Redefinição de senha</h2>
                        <p style="color:#555;line-height:1.6;margin:0 0 24px;">
                          Olá, {user.Name}!<br><br>
                          Recebemos uma solicitação para redefinir a senha da sua conta.
                          Clique no botão abaixo para criar uma nova senha.
                        </p>
                        <table width="100%" cellpadding="0" cellspacing="0">
                          <tr>
                            <td align="center" style="padding:8px 0 32px;">
                              <a href="{resetUrl}"
                                 style="background:#6c63ff;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:6px;font-size:16px;font-weight:bold;display:inline-block;">
                                Redefinir minha senha
                              </a>
                            </td>
                          </tr>
                        </table>
                        <p style="color:#888;font-size:13px;line-height:1.6;margin:0 0 8px;">
                          Se você não solicitou a redefinição, ignore este email — sua senha permanece a mesma.
                        </p>
                        <p style="color:#888;font-size:13px;margin:0;">
                          Este link expira em <strong>1 hora</strong>.
                        </p>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#f9f9f9;padding:24px 48px;border-top:1px solid #eee;">
                        <p style="color:#aaa;font-size:12px;margin:0;text-align:center;">
                          4Sinchrony Experience · contato@4sinchrony.com.br<br>
                          Caso o botão não funcione, copie e cole este link no navegador:<br>
                          <a href="{resetUrl}" style="color:#6c63ff;font-size:11px;word-break:break-all;">{resetUrl}</a>
                        </p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await settingsRepository.GetAsync(CancellationToken.None);
                await emailService.SendWithSettingsAsync(
                    user.Email, subject, body, settings, CancellationToken.None);
            }
            catch
            {
                // SMTP pode falhar silenciosamente
            }
        });
    }
}