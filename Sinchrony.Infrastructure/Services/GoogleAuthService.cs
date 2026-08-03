using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class GoogleAuthService(IConfiguration configuration, ILogger<GoogleAuthService> logger) : IGoogleAuthService
{
    public async Task<GoogleUserInfo> VerifyTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(idToken))
            throw new UnauthorizedAccessException("idToken is required.");

        var clientId = configuration["Google:ClientId"];

        logger.LogInformation("Verifying Google token. ClientId configured: {HasClientId}",
            !string.IsNullOrEmpty(clientId));

        // Se clientId não configurado, valida sem verificar audience (menos seguro, só para dev)
        var settings = string.IsNullOrEmpty(clientId)
            ? new GoogleJsonWebSignature.ValidationSettings { Audience = null }
            : new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            logger.LogInformation("Google token validated for email: {Email}", payload.Email);

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.Name,
                payload.Picture);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogError(ex, "Invalid Google token received.");
            throw new UnauthorizedAccessException("Invalid Google token.", ex);
        }
    }
}