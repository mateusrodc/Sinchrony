using MediatR;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string Token) : IRequest<AuthResponseDto>;

public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    ITokenService tokenService) : IRequestHandler<RefreshTokenCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByRefreshTokenAsync(request.Token, ct)
            ?? throw DomainException.Unauthorized("INVALID_REFRESH_TOKEN");

        var token = user.RefreshTokens.FirstOrDefault(t => t.Token == request.Token);
        if (token is null || !token.IsValid())
            throw DomainException.Unauthorized("INVALID_REFRESH_TOKEN");

        token.Revoke();

        var newAccessToken = tokenService.GenerateAccessToken(user);
        var newRefreshStr = tokenService.GenerateRefreshToken();
        var newRefreshToken = Sinchrony.Domain.Entities.RefreshToken.Create(user.Id, newRefreshStr);

        await userRepository.AddRefreshTokenAsync(newRefreshToken, ct);
        await userRepository.SaveAsync(ct);

        return new AuthResponseDto(
            Token: newAccessToken,
            AccessToken: newAccessToken,
            RefreshToken: newRefreshStr,
            TokenType: "Bearer",
            ExpiresIn: 900);
    }
}