using MediatR;
using Sinchrony.Application.Auth.Commands.Login;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Profile.Commands.UpdateProfile;

public record UpdateProfileCommand(Guid UserId, string Name, string Email, string? Phone, string? Avatar)
    : IRequest<UserDto>;

public class UpdateProfileCommandHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateProfileCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var emailInUse = await userRepository.GetByEmailAsync(request.Email, ct);
        if (emailInUse is not null && emailInUse.Id != request.UserId)
            throw DomainException.Conflict("EMAIL_IN_USE", "Email already in use.");

        user.UpdateProfile(request.Name, request.Email, request.Phone, request.Avatar);
        await userRepository.SaveAsync(ct);

        return new UserDto(user.Id, user.Name, user.Email,
            user.Role.ToString(), user.Credits, user.Phone, user.Avatar);
    }
}