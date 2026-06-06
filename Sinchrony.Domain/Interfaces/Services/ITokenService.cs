using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}