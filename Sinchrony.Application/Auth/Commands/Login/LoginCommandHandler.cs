using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.Login;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    ITokenService tokenService,
    IAuditService auditService) : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct)
            ?? throw DomainException.Unauthorized("Invalid credentials.");

        if (!passwordService.VerifyPassword(request.Password, user.PasswordHash))
        {
            await auditService.LogAsync("auth.login_failed", "User",
                null, null, $"Email: {request.Email}", ct: ct);
            throw DomainException.Unauthorized("Invalid credentials.");
        }

        if (user.Status == StudentStatus.blocked)
            throw DomainException.Forbidden("Account is blocked.");

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshTokenStr = tokenService.GenerateRefreshToken();
        var refreshToken = Sinchrony.Domain.Entities.RefreshToken.Create(user.Id, refreshTokenStr);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveAsync(ct);

        await auditService.LogAsync("auth.login", "User", user.Id, user.Id, ct: ct);

        return BuildResponse(accessToken, refreshTokenStr, user);
    }

    public static AuthResponseDto BuildResponse(string accessToken, string refreshToken, User user) =>
    new(accessToken, accessToken, refreshToken, "Bearer", 900,
        new UserDto(user.Id, user.Name, user.Email,
            user.Role.ToString(), user.Credits, user.Phone, user.Avatar, user.Cpf,
            user.Cep, user.Logradouro, user.Numero,
            user.Complemento, user.Bairro, user.Cidade, user.Estado));
}