namespace Sinchrony.Domain.Entities;

// Aviso persistido pro admin quando uma assinatura recorrente vence (PAYMENT_OVERDUE, via webhook
// ou reconciliação). "Lido" é global: um admin marcando como lido vale pra todos — simplifica e
// basta pra um estúdio só. TransactionId é a chave de idempotência: reprocessar o mesmo evento
// (webhook reenviado, ou reconciliação encontrando o mesmo ciclo vencido) não duplica o alerta.
public class AdminAlert
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = "subscription_overdue";
    public Guid StudentId { get; private set; }
    public Guid StudentPackageId { get; private set; }
    public string TransactionId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; private set; }

    protected AdminAlert() { }

    public static AdminAlert CreateSubscriptionOverdue(
        Guid studentId, Guid studentPackageId, string transactionId, decimal amount)
        => new()
        {
            Type = "subscription_overdue",
            StudentId = studentId,
            StudentPackageId = studentPackageId,
            TransactionId = transactionId,
            Amount = amount
        };

    public void MarkRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }
}
