using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Auth.Commands.Logout;

public record LogoutCommand(Guid UserId) : IRequest;

public class LogoutCommandHandler(IUserRepository userRepository) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        foreach (var token in user.RefreshTokens.Where(t => !t.Revoked))
            token.Revoke();

        await userRepository.SaveAsync(ct);
    }
}