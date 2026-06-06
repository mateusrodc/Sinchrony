using FluentValidation;
using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.ChangePassword;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest;

public class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordService passwordService) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        if (!passwordService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw DomainException.Validation("INVALID_CURRENT_PASSWORD", "Current password is incorrect.");

        var newHash = passwordService.HashPassword(request.NewPassword);
        user.ChangePassword(newHash);

        foreach (var token in user.RefreshTokens.Where(t => !t.Revoked))
            token.Revoke();

        await userRepository.SaveAsync(ct);
    }
}

public class ChangePasswordCommandValidator : FluentValidation.AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}