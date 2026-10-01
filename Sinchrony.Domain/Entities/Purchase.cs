namespace Sinchrony.Domain.Entities;

public class Purchase
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid PackageId { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? CouponId { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty; // pix | card | cash | courtesy (cash/courtesy só no balcão)

    // Canal onde a compra foi feita: "app" (aluno pagou pelo aplicativo) | "balcao" (recepção
    // lançou pelo ERP, em qualquer método: dinheiro, cortesia, pix ou cartão da maquininha).
    // Independe do Status — "confirmed" vale para as duas origens.
    public string Channel { get; private set; } = "app";

    public string Status { get; private set; } = "confirmed";
    public string? TransactionId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // Preenchido só para compras vinculadas a um pacote recorrente (contratação inicial ou
    // renovação automática) — permite montar o histórico de pagamentos de uma assinatura.
    public Guid? StudentPackageId { get; private set; }

    // "purchase" (contratação/compra avulsa) | "renewal" (cobrança automática de um ciclo
    // seguinte, gerada pelo RecurringRenewalService). Só existe pra diferenciar no histórico —
    // não afeta o fluxo de confirmação/falha em si.
    public string Kind { get; private set; } = "purchase";

    public User? User { get; private set; }
    public Package? Package { get; private set; }
    public Coupon? Coupon { get; private set; }
    public StudentPackage? StudentPackage { get; private set; }

    protected Purchase() { }

    public static Purchase Create(Guid userId, Guid packageId, decimal amount,
        string paymentMethod, string? transactionId = null, Guid? couponId = null,
        Guid? studentPackageId = null)
        => new()
        {
            UserId = userId,
            PackageId = packageId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            TransactionId = transactionId,
            CouponId = couponId,
            StudentPackageId = studentPackageId
        };

    public void Confirm()
    {
        Status = "confirmed";
    }

    public void Fail()
    {
        Status = "failed";
    }

    public static Purchase CreatePending(Guid userId, Guid packageId, decimal amount,
    string paymentMethod, string? transactionId = null, Guid? couponId = null,
    Guid? studentPackageId = null)
    => new()
    {
        UserId = userId,
        PackageId = packageId,
        Amount = amount,
        PaymentMethod = paymentMethod,
        TransactionId = transactionId,
        CouponId = couponId,
        StudentPackageId = studentPackageId,
        Status = "pending"  // aguarda webhook
    };

    public static Purchase CreateConfirmed(
    Guid userId, Guid packageId, decimal amount,
    string paymentMethod, string? transactionId, Guid? studentPackageId = null)
    {
        var purchase = CreatePending(userId, packageId, amount, paymentMethod, transactionId, null, studentPackageId);
        purchase.Confirm();
        return purchase;
    }

    // Concessão feita no balcão (ERP): já nasce confirmada e com canal "balcao".
    public static Purchase CreateCounterConfirmed(
        Guid userId, Guid packageId, decimal amount, string paymentMethod)
    {
        var purchase = CreateConfirmed(userId, packageId, amount, paymentMethod, null);
        purchase.Channel = "balcao";
        return purchase;
    }

    // Cobrança automática de um ciclo seguinte de um pacote recorrente (RecurringRenewalService).
    public static Purchase CreateRenewalPending(
        Guid userId, Guid packageId, Guid studentPackageId, decimal amount, string? transactionId)
    {
        var purchase = CreatePending(userId, packageId, amount, "card", transactionId, null, studentPackageId);
        purchase.Kind = "renewal";
        return purchase;
    }
}