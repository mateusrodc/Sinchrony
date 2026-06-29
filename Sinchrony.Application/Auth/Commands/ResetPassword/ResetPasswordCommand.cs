using FluentValidation;
using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(string Token, string NewPassword) : IRequest;

public class ResetPasswordCommandHandler(
    IPasswordResetTokenRepository tokenRepository,
    IUserRepository userRepository,
    IPasswordService passwordService) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var resetToken = await tokenRepository.GetByTokenAsync(request.Token, ct);

        if (resetToken is null || !resetToken.IsValid())
            throw DomainException.Validation(
                resetToken is null ? "TOKEN_INVALID" : "TOKEN_EXPIRED",
                resetToken is null ? "Token inválido." : "Token expirado.");

        var user = await userRepository.GetByIdAsync(resetToken.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var newHash = passwordService.HashPassword(request.NewPassword);
        user.ChangePassword(newHash);
        resetToken.MarkUsed();

        // Revoga todos os refresh tokens
        foreach (var token in user.RefreshTokens.Where(t => !t.Revoked))
            token.Revoke();

        await userRepository.SaveAsync(ct);
        await tokenRepository.SaveAsync(ct);
    }
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}