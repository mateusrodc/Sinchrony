namespace Sinchrony.Domain.Entities;

public class Coupon
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public decimal Discount { get; private set; }
    public string DiscountType { get; private set; } = "percentage"; // percentage | fixed
    public bool Active { get; private set; } = true;
    public DateTime? ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    protected Coupon() { }

    public static Coupon Create(string code, decimal discount, string discountType, DateTime? expiresAt = null)
        => new() { Code = code.ToUpper(), Discount = discount, DiscountType = discountType, ExpiresAt = expiresAt };

    public bool IsValid() => Active && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}