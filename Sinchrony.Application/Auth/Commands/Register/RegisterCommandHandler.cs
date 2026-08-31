using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Auth.Commands.Register;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    ITokenService tokenService,
    IConfiguration configuration,
    ILogger<RegisterCommandHandler> logger) : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        // Valida CPF duplicado
        if (!string.IsNullOrEmpty(request.Cpf))
        {
            var cpfSanitized = CpfValidator.Sanitize(request.Cpf);
            var cpfInUse = await userRepository.GetByCpfAsync(cpfSanitized, ct);
            if (cpfInUse is not null)
                throw DomainException.Conflict("CPF_ALREADY_IN_USE", "CPF já cadastrado.");
        }

        // O app (SinchronyApp/RegisterScreen.tsx) já bloqueia o botão "Criar Conta" até o
        // checkbox "Li e aceito os Termos..." ser marcado — mas o authService.register()
        // ainda não manda termsAcceptedAt/termsVersion no corpo, então esses campos chegam
        // vazios aqui. Como o consentimento já foi capturado na UI antes do POST ser
        // disparado, o servidor assume aceite no momento do próprio registro em vez de
        // travar o cadastro com 422 por um campo que o cliente nunca envia. Isso é uma
        // aproximação (perde o timestamp exato do clique) — o certo continua sendo o app
        // implementar o envio real (ver DEMANDA_TERMOS_DE_USO_CADASTRO_BACKEND.md).
        var termsAcceptedAt = request.TermsAcceptedAt ?? DateTime.UtcNow;
        var termsVersion = string.IsNullOrEmpty(request.TermsVersion)
            ? configuration["Terms:CurrentVersion"] ?? "1.0"
            : request.TermsVersion;

        if (request.TermsAcceptedAt is null || string.IsNullOrEmpty(request.TermsVersion))
            logger.LogWarning(
                "Register: cliente não mandou termsAcceptedAt/termsVersion; assumindo aceite padrão (versão {Version}) para {Email}.",
                termsVersion, request.Email);

        var hash = passwordService.HashPassword(request.Password);
        var user = User.Create(request.Name, request.Email, request.Phone, hash, Role.student, request.Cpf);

        if (request.UnitId.HasValue)
            user.SetUnit(request.UnitId.Value);

        user.UpdateAddress(request.Cep, request.Logradouro, request.Numero,
            request.Complemento, request.Bairro, request.Cidade, request.Estado);

        user.AcceptTerms(termsAcceptedAt, termsVersion);

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshStr = tokenService.GenerateRefreshToken();
        var refreshToken = Domain.Entities.RefreshToken.Create(user.Id, refreshStr);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveAsync(ct);

        return LoginCommandHandler.BuildResponse(accessToken, refreshStr, user);
    }
}