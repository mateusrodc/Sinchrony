namespace Sinchrony.Domain.Interfaces.Services;

public record PixPaymentResult(string TransactionId, string PixCode, string QrCodeBase64);
public record CardPaymentResult(string TransactionId, string Status, string Message);
public record CardTokenizationResult(string Token, string LastDigits, string Brand);
public record SubscriptionResult(string SubscriptionId, string Status);

public interface IAsaasService
{
    Task<string> GetOrCreateCustomerAsync(string name, string email, string? cpf = null, CancellationToken ct = default);
    Task<PixPaymentResult> CreatePixChargeAsync(string customerId, decimal amount, string description, CancellationToken ct = default);
    Task<CardPaymentResult> ChargeCardAsync(string customerId, string cardToken, decimal amount, string description, CancellationToken ct = default);
    Task<CardTokenizationResult> TokenizeCardAsync(
    string number, string holderName, string expiryDate,
    string cvv, string customerId, string cpf,
    string remoteIp, string postalCode, string addressNumber,
    string? addressComplement, string? email, string? phone,
    CancellationToken ct = default);

    // Assinatura recorrente (cobrança mensal automática pela própria Asaas).
    Task<SubscriptionResult> CreateSubscriptionAsync(
        string customerId, string cardToken, decimal amount, string description, CancellationToken ct = default);

    // Troca o cartão usado por uma assinatura já criada (ex.: aluno bloqueado regulariza o cadastro).
    Task UpdateSubscriptionCardAsync(string subscriptionId, string cardToken, CancellationToken ct = default);
}