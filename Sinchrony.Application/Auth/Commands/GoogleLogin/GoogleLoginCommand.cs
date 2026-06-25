using MediatR;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.GoogleLogin;

public record GoogleLoginCommand(string IdToken) : IRequest<AuthResponseDto>;

public class GoogleLoginCommandHandler(
    IGoogleAuthService googleAuthService,
    IUserRepository userRepository,
    ITokenService tokenService,
    IPasswordService passwordService,
    IAuditService auditService) : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        GoogleUserInfo googleUser;
        try
        {
            googleUser = await googleAuthService.VerifyTokenAsync(request.IdToken, ct);
        }
        catch (UnauthorizedAccessException)
        {
            throw DomainException.Unauthorized("Invalid Google token.");
        }

        var user = await userRepository.GetByGoogleIdAsync(googleUser.GoogleId, ct);

        if (user is null)
        {
            user = await userRepository.GetByEmailAsync(googleUser.Email, ct);

            if (user is null)
            {
                var randomHash = passwordService.HashPassword(Guid.NewGuid().ToString());
                user = User.CreateWithGoogle(
                    googleUser.Name, googleUser.Email,
                    googleUser.GoogleId, googleUser.Picture);
                user.ChangePassword(randomHash);

                await userRepository.AddAsync(user, ct);
                await userRepository.SaveAsync(ct);
            }
            else
            {
                user.LinkGoogle(googleUser.GoogleId);
                await userRepository.SaveAsync(ct);
            }
        }

        if (user.Status == StudentStatus.blocked)
            throw DomainException.Forbidden("Account is blocked.");

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshStr = tokenService.GenerateRefreshToken();
        var refreshToken = Sinchrony.Domain.Entities.RefreshToken.Create(user.Id, refreshStr);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveAsync(ct);

        await auditService.LogAsync("auth.google_login", "User", user.Id, user.Id, ct: ct);

        return LoginCommandHandler.BuildResponse(accessToken, refreshStr, user);
    }
}