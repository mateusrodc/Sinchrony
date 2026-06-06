using MediatR;
using Sinchrony.Application.Auth.Commands.Login;

namespace Sinchrony.Application.Auth.Commands.Register;

public record RegisterCommand(string Name, string Email, string? Phone, string Password) : IRequest<AuthResponseDto>;