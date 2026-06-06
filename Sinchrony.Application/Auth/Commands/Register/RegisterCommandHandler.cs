using MediatR;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.Register;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService,
    ITokenService tokenService) : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(request.Email, ct);
        if (existing is not null)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        var hash = passwordService.HashPassword(request.Password);
        var user = User.Create(request.Name, request.Email, request.Phone, hash, Role.student);

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshStr = tokenService.GenerateRefreshToken();
        var refreshToken = Sinchrony.Domain.Entities.RefreshToken.Create(user.Id, refreshStr);

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveAsync(ct);

        return LoginCommandHandler.BuildResponse(accessToken, refreshStr, user);
    }
}