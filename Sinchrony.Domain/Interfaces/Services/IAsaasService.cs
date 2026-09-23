namespace Sinchrony.Domain.Interfaces.Services;

public record PixPaymentResult(string TransactionId, string PixCode, string QrCodeBase64);
public record CardPaymentResult(string TransactionId, string Status, string Message);
public record CardTokenizationResult(string Token, string LastDigits, string Brand);

// Status de uma cobrança consultada diretamente na Asaas — usado pra reconciliar uma renovação
// que ficou "pending" (antifraude) e cujo webhook de confirmação/recusa nunca chegou.
public record PaymentStatusResult(string Status, string? FailureReason);

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

    // Consulta o status de uma cobrança avulsa/renovação pelo id do pagamento — usado pela
    // reconciliação de uma renovação "pending" cujo webhook se perdeu.
    Task<PaymentStatusResult> GetPaymentAsync(string paymentId, CancellationToken ct = default);

    // --- Legado: assinatura Asaas (POST /v3/subscriptions), modelo abandonado em 23/09 em favor
    // de renovação automática cobrada pelo próprio Sinchrony (ver RecurringRenewalService).
    // Mantidos só como rede de segurança caso sobre algum registro com AsaasSubscriptionId.

    // Troca o cartão usado por uma assinatura já criada (ex.: aluno bloqueado regulariza o cadastro).
    Task UpdateSubscriptionCardAsync(string subscriptionId, string cardToken, CancellationToken ct = default);

    // Encerra a assinatura na Asaas (ela para de cobrar o cartão). Chamado sempre que o
    // StudentPackage correspondente é cancelado/substituído, senão a Asaas continua cobrando
    // mesmo com o pacote já cancelado no nosso banco.
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
}
