using Microsoft.Extensions.Configuration;
using Sinchrony.Domain.Interfaces.Services;
using Google.Apis.Auth;

namespace Sinchrony.Infrastructure.Services;

public class GoogleAuthService(IConfiguration configuration) : IGoogleAuthService
{
    public async Task<GoogleUserInfo> VerifyTokenAsync(string idToken, CancellationToken ct = default)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [configuration["Google:ClientId"]!]
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.Name,
                payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            throw new UnauthorizedAccessException("Invalid Google token.", ex);
        }
    }
}