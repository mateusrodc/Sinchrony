using MediatR;

namespace Sinchrony.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;

public record AuthResponseDto(
    string Token,
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    UserDto? User = null);

public record UserDto(
    Guid Id, string Name, string Email, string Role,
    int Credits, string? Phone, string? Avatar, string? Cpf);