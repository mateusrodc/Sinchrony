namespace Sinchrony.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool Revoked { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public User? User { get; private set; }

    protected RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, int validityDays = 7)
        => new() { UserId = userId, Token = token, ExpiresAt = DateTime.UtcNow.AddDays(validityDays) };

    public bool IsValid() => !Revoked && ExpiresAt > DateTime.UtcNow;
    public void Revoke() => Revoked = true;
}