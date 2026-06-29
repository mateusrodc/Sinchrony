using FluentValidation;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Auth.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Cpf)
            .Must(cpf => CpfValidator.IsValid(cpf))
            .When(x => !string.IsNullOrEmpty(x.Cpf))
            .WithMessage("CPF inválido.");
    }
}