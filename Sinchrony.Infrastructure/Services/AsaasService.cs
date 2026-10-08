using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sinchrony.Infrastructure.Services;

public class AsaasService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AsaasService> logger) : IAsaasService
{
    private readonly bool _sandbox = configuration.GetValue<bool>("Asaas:Sandbox", true);

    private string BaseUrl => _sandbox
        ? "https://sandbox.asaas.com/api/v3"
        : "https://api.asaas.com/v3";

    private static string ExtractErrorDescription(string content, string fallback)
    {
        try
        {
            var errorBody = JsonSerializer.Deserialize<JsonElement>(content);
            return errorBody.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                ? errors[0].GetProperty("description").GetString() ?? fallback
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Campos do cliente Asaas a enviar; só entram os preenchidos no cadastro do aluno.
    private static Dictionary<string, string> BuildCustomerFields(AsaasCustomerData c)
    {
        var fields = new Dictionary<string, string>();
        void Add(string key, string? value) { if (value is not null) fields[key] = value; }

        var phone = Digits(c.MobilePhone);
        if (phone is { Length: > 11 } && phone.StartsWith("55")) phone = phone[2..];

        Add("postalCode", Digits(c.PostalCode));
        Add("address", Clean(c.Address));
        Add("addressNumber", Clean(c.AddressNumber));
        Add("complement", Clean(c.Complement));
        Add("province", Clean(c.Province));
        Add("mobilePhone", phone);
        Add("externalReference", Clean(c.ExternalReference));
        return fields;
    }

    private static string? ReadString(JsonElement customer, string property)
        => customer.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    // Compara só os campos que o aluno tem preenchidos; devolve os que diferem ou estão em branco na Asaas.
    private static Dictionary<string, string> DiffCustomerFields(
        JsonElement existing, Dictionary<string, string> desired)
    {
        var diff = new Dictionary<string, string>();
        foreach (var (key, value) in desired)
        {
            var current = ReadString(existing, key);
            var same = key is "postalCode" or "mobilePhone"
                ? Digits(current) == value
                : string.Equals(Clean(current), value, StringComparison.OrdinalIgnoreCase);
            if (!same) diff[key] = value;
        }
        return diff;
    }

    private async Task TryUpdateCustomerAsync(
        string customerId, Dictionary<string, string> fields, CancellationToken ct)
    {
        try
        {
            var resp = await httpClient.PutAsJsonAsync($"{BaseUrl}/customers/{customerId}", fields, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Asaas: failed to update customer {CustomerId}. Status: {Status}, Body: {Body}",
                    customerId, resp.StatusCode, content);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asaas: failed to update customer {CustomerId}", customerId);
        }
    }

    public async Task<string> GetOrCreateCustomerAsync(
        AsaasCustomerData customer, CancellationToken ct = default)
    {
        var fields = BuildCustomerFields(customer);

        try
        {
            // Busca cliente existente
            var resp = await httpClient.GetAsync(
                $"{BaseUrl}/customers?email={Uri.EscapeDataString(customer.Email)}&limit=1", ct);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                var data = json.GetProperty("data");
                if (data.GetArrayLength() > 0)
                {
                    var existing = data[0];
                    var customerId = existing.GetProperty("id").GetString()!;

                    var diff = DiffCustomerFields(existing, fields);
                    if (diff.Count > 0)
                        await TryUpdateCustomerAsync(customerId, diff, ct);

                    return customerId;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asaas: failed to search customer by email {Email}", customer.Email);
        }

        // Cria novo cliente
        var body = new Dictionary<string, string>(fields)
        {
            ["name"] = customer.Name,
            ["email"] = customer.Email
        };
        if (!string.IsNullOrWhiteSpace(customer.Cpf))
            body["cpfCnpj"] = customer.Cpf;

        var createResp = await httpClient.PostAsJsonAsync($"{BaseUrl}/customers", body, ct);
        var content = await createResp.Content.ReadAsStringAsync(ct);

        if (!createResp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to create customer. Status: {Status}, Body: {Body}",
                createResp.StatusCode, content);

            var description = ExtractErrorDescription(content, "Erro ao criar cliente na Asaas.");
            throw DomainException.Validation("ASAAS_CUSTOMER_ERROR", description);
        }

        var created = JsonSerializer.Deserialize<JsonElement>(content);
        return created.GetProperty("id").GetString()!;
    }

    public async Task<PixPaymentResult> CreatePixChargeAsync(
        string customerId, decimal amount, string description, CancellationToken ct = default)
    {
        var dueDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        var body = new
        {
            customer = customerId,
            billingType = "PIX",
            value = amount,
            dueDate,
            description
        };

        var resp = await httpClient.PostAsJsonAsync($"{BaseUrl}/payments", body, ct);
        var content = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to create PIX charge. Status: {Status}, Body: {Body}",
                resp.StatusCode, content);

            var errorDescription = ExtractErrorDescription(content, "Erro ao gerar cobrança PIX.");
            throw DomainException.Validation("ASAAS_PIX_ERROR", errorDescription);
        }

        var payment = JsonSerializer.Deserialize<JsonElement>(content);
        var transactionId = payment.GetProperty("id").GetString()!;

        return await GetPixQrCodeAsync(transactionId, ct);
    }

    public async Task<PixPaymentResult> GetPixQrCodeAsync(string transactionId, CancellationToken ct = default)
    {
        string pixCode = string.Empty;
        string qrCodeBase64 = string.Empty;

        try
        {
            var qrResp = await httpClient.GetAsync(
                $"{BaseUrl}/payments/{transactionId}/pixQrCode", ct);

            if (qrResp.IsSuccessStatusCode)
            {
                var qr = await qrResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                pixCode = qr.TryGetProperty("payload", out var payload)
                    ? payload.GetString() ?? string.Empty
                    : string.Empty;
                qrCodeBase64 = qr.TryGetProperty("encodedImage", out var img)
                    ? img.GetString() ?? string.Empty
                    : string.Empty;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asaas: failed to fetch PIX QR Code for payment {Id}", transactionId);
        }

        return new PixPaymentResult(transactionId, pixCode, qrCodeBase64);
    }

    public async Task CancelPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        var resp = await httpClient.DeleteAsync($"{BaseUrl}/payments/{paymentId}", ct);

        if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return;

        var content = await resp.Content.ReadAsStringAsync(ct);
        logger.LogError("Asaas: failed to cancel payment {Id}. Status: {Status}, Body: {Body}",
            paymentId, resp.StatusCode, content);

        var description = ExtractErrorDescription(content, "Erro ao cancelar cobrança.");
        throw DomainException.Validation("ASAAS_PAYMENT_CANCEL_ERROR", description);
    }

    public async Task<CardPaymentResult> ChargeCardAsync(
        string customerId, string cardToken, decimal amount,
        string description, CancellationToken ct = default)
    {
        var body = new
        {
            customer = customerId,
            billingType = "CREDIT_CARD",
            value = amount,
            dueDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            description,
            creditCardToken = cardToken
        };

        var resp = await httpClient.PostAsJsonAsync($"{BaseUrl}/payments", body, ct);
        var content = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to charge card. Status: {Status}, Body: {Body}",
                resp.StatusCode, content);

            var errorDescription = ExtractErrorDescription(content, "Erro ao processar pagamento no cartão.");
            throw DomainException.Validation("ASAAS_CARD_CHARGE_ERROR", errorDescription);
        }

        var payment = JsonSerializer.Deserialize<JsonElement>(content);
        var status = payment.GetProperty("status").GetString()!;
        var message = status == "PENDING"
            ? "Pagamento em análise. Você será notificado assim que for confirmado."
            : "Pagamento no cartão aprovado!";

        return new CardPaymentResult(
            payment.GetProperty("id").GetString()!,
            status,
            message);
    }

    public async Task<PaymentStatusResult> GetPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        var resp = await httpClient.GetAsync($"{BaseUrl}/payments/{paymentId}", ct);

        // 404: a cobrança foi removida na Asaas (ex.: cancelada por CancelPaymentAsync). Mesmo status
        // que a Asaas usa pra cobrança apagada, que os chamadores já tratam como "não paga".
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new PaymentStatusResult("PAYMENT_DELETED", null);
        var content = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to fetch payment {Id}. Status: {Status}, Body: {Body}",
                paymentId, resp.StatusCode, content);

            var description = ExtractErrorDescription(content, "Erro ao consultar cobrança.");
            throw DomainException.Validation("ASAAS_PAYMENT_FETCH_ERROR", description);
        }

        // payment.description é só o texto que o Sinchrony mesmo mandou ao criar a cobrança
        // ("4Sinchrony - X (renovação)") — a Asaas não devolve um motivo de recusa dedicado
        // aqui. Usar a description como motivo seria enganoso, melhor null.
        var payment = JsonSerializer.Deserialize<JsonElement>(content);
        return new PaymentStatusResult(payment.GetProperty("status").GetString()!, null);
    }

    public async Task UpdateSubscriptionCardAsync(string subscriptionId, string cardToken, CancellationToken ct = default)
    {
        var body = new { creditCardToken = cardToken };

        var resp = await httpClient.PutAsJsonAsync($"{BaseUrl}/subscriptions/{subscriptionId}", body, ct);
        var content = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to update subscription card. Status: {Status}, Body: {Body}",
                resp.StatusCode, content);

            var description = ExtractErrorDescription(content, "Erro ao atualizar cartão da assinatura.");
            throw DomainException.Validation("ASAAS_SUBSCRIPTION_CARD_UPDATE_ERROR", description);
        }
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        var resp = await httpClient.DeleteAsync($"{BaseUrl}/subscriptions/{subscriptionId}", ct);

        // 404: a assinatura já não existe mais na Asaas (ex.: cancelada por lá diretamente) —
        // o objetivo (parar de cobrar) já está atingido, trata como sucesso (idempotente).
        if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return;

        var content = await resp.Content.ReadAsStringAsync(ct);
        logger.LogError("Asaas: failed to cancel subscription {Id}. Status: {Status}, Body: {Body}",
            subscriptionId, resp.StatusCode, content);

        var description = ExtractErrorDescription(content, "Erro ao cancelar assinatura recorrente.");
        throw DomainException.Validation("ASAAS_SUBSCRIPTION_CANCEL_ERROR", description);
    }

    public async Task<CardTokenizationResult> TokenizeCardAsync(
    string number, string holderName, string expiryDate,
    string cvv, string customerId, string cpf,
    string remoteIp, string postalCode, string addressNumber,
    string? addressComplement, string? email, string? phone,
    CancellationToken ct = default)
    {
        var parts = expiryDate.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException("ExpiryDate must be in MM/YY format.");

        var cpfLimpo = string.IsNullOrEmpty(cpf)
            ? "00000000000"
            : cpf.Replace(".", "").Replace("-", "").Replace("/", "").Trim();

        var cepLimpo = postalCode.Replace("-", "").Trim();

        var body = new
        {
            customer = customerId,
            creditCard = new
            {
                holderName,
                number = number.Replace(" ", ""),
                expiryMonth = parts[0],
                expiryYear = parts[1].Length == 2 ? "20" + parts[1] : parts[1],
                ccv = cvv
            },
            creditCardHolderInfo = new
            {
                name = holderName,
                email = email ?? "noreply@sinchrony.com",
                cpfCnpj = cpfLimpo,
                postalCode = cepLimpo,
                addressNumber,
                addressComplement,
                phone = phone ?? "11999999999"
            },
            remoteIp
        };

        var resp = await httpClient.PostAsJsonAsync(
            $"{BaseUrl}/creditCard/tokenizeCreditCard", body, ct);
        var content = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to tokenize card. Status: {Status}, Body: {Body}",
                resp.StatusCode, content);

            // Trata erro do Asaas como 422, não 500
            var errorBody = JsonSerializer.Deserialize<JsonElement>(content);
            var description = errorBody.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                ? errors[0].GetProperty("description").GetString()
                : "Erro ao tokenizar cartão.";

            throw DomainException.Validation("CARD_TOKENIZATION_ERROR", description!);
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return new CardTokenizationResult(
            result.GetProperty("creditCardToken").GetString()!,
            result.GetProperty("creditCardNumber").GetString()!,
            result.GetProperty("creditCardBrand").GetString()!);
    }
}