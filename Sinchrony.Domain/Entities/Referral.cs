namespace Sinchrony.Domain.Entities;

public class Referral
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ReferrerId { get; private set; }
    public Guid ReferredId { get; private set; }
    public int CreditsEarned { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public User? Referrer { get; private set; }
    public User? Referred { get; private set; }

    protected Referral() { }

    public static Referral Create(Guid referrerId, Guid referredId, int credits)
        => new() { ReferrerId = referrerId, ReferredId = referredId, CreditsEarned = credits };
}