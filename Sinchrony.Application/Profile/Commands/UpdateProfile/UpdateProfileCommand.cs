using MediatR;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Services;

namespace Sinchrony.Application.Profile.Commands.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId, string Name, string Email,
    string? Phone, string? Avatar, string? Cpf,
    string? Cep, string? Logradouro, string? Numero,
    string? Complemento, string? Bairro, string? Cidade, string? Estado,
    Guid? UnitId) : IRequest<UserDto>;

public class UpdateProfileCommandHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateProfileCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        user.UpdateAddress(request.Cep, request.Logradouro, request.Numero,
    request.Complemento, request.Bairro, request.Cidade, request.Estado);

        if (request.UnitId.HasValue)
            user.SetUnit(request.UnitId.Value);

        var emailInUse = await userRepository.GetByEmailAsync(request.Email, ct);
        if (emailInUse is not null && emailInUse.Id != request.UserId)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        if (!string.IsNullOrEmpty(request.Cpf) && !CpfValidator.IsValid(request.Cpf))
            throw DomainException.Validation("INVALID_CPF", "CPF inválido.");

        user.UpdateProfile(request.Name, request.Email, request.Phone, request.Avatar);
        if (request.Cpf is not null) user.UpdateCpf(request.Cpf);

        await userRepository.SaveAsync(ct);

        return new UserDto(user.Id, user.Name, user.Email,
    user.Role.ToString(), user.Credits, user.Phone, user.Avatar, user.Cpf,
    user.Cep, user.Logradouro, user.Numero,
    user.Complemento, user.Bairro, user.Cidade, user.Estado);
    }
}