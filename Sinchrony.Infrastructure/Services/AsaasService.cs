using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class AsaasService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AsaasService> logger) : IAsaasService
{
    private readonly bool _sandbox = configuration.GetValue<bool>("Asaas:Sandbox", true);

    private string BaseUrl => _sandbox
        ? "https://sandbox.asaas.com/api/v3"
        : "https://api.asaas.com/api/v3";

    public async Task<string> GetOrCreateCustomerAsync(
        string name, string email, string? cpf = null, CancellationToken ct = default)
    {
        try
        {
            // Busca cliente existente
            var resp = await httpClient.GetAsync(
                $"{BaseUrl}/customers?email={Uri.EscapeDataString(email)}&limit=1", ct);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                var data = json.GetProperty("data");
                if (data.GetArrayLength() > 0)
                    return data[0].GetProperty("id").GetString()!;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asaas: failed to search customer by email {Email}", email);
        }

        // Cria novo cliente
        var body = new
        {
            name,
            email,
            cpfCnpj = cpf
        };

        var createResp = await httpClient.PostAsJsonAsync($"{BaseUrl}/customers", body, ct);
        var content = await createResp.Content.ReadAsStringAsync(ct);

        if (!createResp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to create customer. Status: {Status}, Body: {Body}",
                createResp.StatusCode, content);
            throw new InvalidOperationException($"Asaas customer creation failed: {content}");
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
            throw new InvalidOperationException($"Asaas PIX charge failed: {content}");
        }

        var payment = JsonSerializer.Deserialize<JsonElement>(content);
        var transactionId = payment.GetProperty("id").GetString()!;

        // Busca QR Code PIX
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
            throw new InvalidOperationException($"Asaas card charge failed: {content}");
        }

        var payment = JsonSerializer.Deserialize<JsonElement>(content);
        return new CardPaymentResult(
            payment.GetProperty("id").GetString()!,
            "Pagamento no cartão aprovado!");
    }

    public async Task<CardTokenizationResult> TokenizeCardAsync(
        string number, string holderName, string expiryDate,
        string cvv, string customerId, CancellationToken ct = default)
    {
        var parts = expiryDate.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException("ExpiryDate must be in MM/YY format.");

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
                email = "noreply@sinchrony.com",
                // Campos mínimos exigidos pelo Asaas
                cpfCnpj = "00000000000", // placeholder — idealmente pegar do usuário
                postalCode = "00000000",
                addressNumber = "0",
                phone = "00000000000"
            }
        };

        var resp = await httpClient.PostAsJsonAsync($"{BaseUrl}/creditCards/tokenize", body, ct);
        var content = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Asaas: failed to tokenize card. Status: {Status}, Body: {Body}",
                resp.StatusCode, content);
            throw new InvalidOperationException($"Asaas card tokenization failed: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return new CardTokenizationResult(
            result.GetProperty("creditCardToken").GetString()!,
            result.GetProperty("creditCardNumber").GetString()!,
            result.GetProperty("creditCardBrand").GetString()!);
    }
}