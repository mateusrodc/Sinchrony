namespace Sinchrony.Domain.Entities;

public class CreditTransaction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public int Amount { get; private set; }        // positivo = crédito, negativo = débito
    public int BalanceAfter { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty; // purchase | booking | refund | manual
    public Guid? ReferenceId { get; private set; } // BookingId ou PurchaseId
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public User? User { get; private set; }

    protected CreditTransaction() { }

    public static CreditTransaction Create(
        Guid userId, int amount, int balanceAfter,
        string reason, string type, Guid? referenceId = null)
        => new()
        {
            UserId = userId,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Reason = reason,
            Type = type,
            ReferenceId = referenceId
        };
}