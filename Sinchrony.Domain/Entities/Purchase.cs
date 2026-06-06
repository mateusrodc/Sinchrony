namespace Sinchrony.Domain.Entities;

public class Purchase
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid PackageId { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? CouponId { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty; // pix | card
    public string Status { get; private set; } = "confirmed";
    public string? TransactionId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public User? User { get; private set; }
    public Package? Package { get; private set; }
    public Coupon? Coupon { get; private set; }

    protected Purchase() { }

    public static Purchase Create(Guid userId, Guid packageId, decimal amount,
        string paymentMethod, string? transactionId = null, Guid? couponId = null)
        => new()
        {
            UserId = userId,
            PackageId = packageId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            TransactionId = transactionId,
            CouponId = couponId
        };

    public void Confirm()
    {
        Status = "confirmed";
    }

    public static Purchase CreatePending(Guid userId, Guid packageId, decimal amount,
    string paymentMethod, string? transactionId = null, Guid? couponId = null)
    => new()
    {
        UserId = userId,
        PackageId = packageId,
        Amount = amount,
        PaymentMethod = paymentMethod,
        TransactionId = transactionId,
        CouponId = couponId,
        Status = "pending"  // aguarda webhook
    };
}