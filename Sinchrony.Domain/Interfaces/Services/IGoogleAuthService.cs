namespace Sinchrony.Domain.Interfaces.Services;

public record GoogleUserInfo(string GoogleId, string Email, string Name, string? Picture);

public interface IGoogleAuthService
{
    Task<GoogleUserInfo> VerifyTokenAsync(string idToken, CancellationToken ct = default);
}