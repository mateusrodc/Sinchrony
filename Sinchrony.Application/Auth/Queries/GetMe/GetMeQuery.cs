using MediatR;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Auth.Queries.GetMe;

public record GetMeQuery(Guid UserId) : IRequest<UserDto>;

public class GetMeQueryHandler(IUserRepository userRepository) : IRequestHandler<GetMeQuery, UserDto>
{
    public async Task<UserDto> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        return new UserDto(user.Id, user.Name, user.Email,
    user.Role.ToString(), user.Credits, user.Phone, user.Avatar, user.Cpf,
    user.Cep, user.Logradouro, user.Numero,
    user.Complemento, user.Bairro, user.Cidade, user.Estado);
    }
}