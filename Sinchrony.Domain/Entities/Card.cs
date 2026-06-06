namespace Sinchrony.Domain.Entities;

public class Card
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string LastDigits { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public string HolderName { get; private set; } = string.Empty;
    public string ExpiryDate { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public string? Nickname { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public User? User { get; private set; }

    protected Card() { }

    public static Card Create(Guid userId, string lastDigits, string brand,
        string holderName, string expiryDate, string token, string? nickname = null)
        => new()
        {
            UserId = userId,
            LastDigits = lastDigits,
            Brand = brand,
            HolderName = holderName,
            ExpiryDate = expiryDate,
            Token = token,
            Nickname = nickname
        };

    public void SetAsDefault() => IsDefault = true;
    public void RemoveDefault() => IsDefault = false;
}