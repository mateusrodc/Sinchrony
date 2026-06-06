using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class AsaasService(HttpClient httpClient, IConfiguration configuration) : IAsaasService
{
    private readonly bool _sandbox = configuration.GetValue<bool>("Asaas:Sandbox", true);

    private string BaseUrl => _sandbox
        ? "https://sandbox.asaas.com/api/v3"
        : "https://api.asaas.com/api/v3";

    public async Task<string> GetOrCreateCustomerAsync(string name, string email, string? cpf = null, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"{BaseUrl}/customers?email={Uri.EscapeDataString(email)}", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        if (json.GetProperty("data").GetArrayLength() > 0)
            return json.GetProperty("data")[0].GetProperty("id").GetString()!;

        var body = new { name, email, cpfCnpj = cpf };
        var createResp = await httpClient.PostAsJsonAsync($"{BaseUrl}/customers", body, ct);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return created.GetProperty("id").GetString()!;
    }

    public async Task<PixPaymentResult> CreatePixChargeAsync(string customerId, decimal amount, string description, CancellationToken ct = default)
    {
        var body = new
        {
            customer = customerId,
            billingType = "PIX",
            value = amount,
            dueDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
            description
        };

        var resp = await httpClient.PostAsJsonAsync($"{BaseUrl}/payments", body, ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var transactionId = data.GetProperty("id").GetString()!;

        var qrResp = await httpClient.GetAsync($"{BaseUrl}/payments/{transactionId}/pixQrCode", ct);
        qrResp.EnsureSuccessStatusCode();
        var qr = await qrResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        return new PixPaymentResult(
            transactionId,
            qr.GetProperty("payload").GetString()!,
            qr.GetProperty("encodedImage").GetString()!);
    }

    public async Task<CardPaymentResult> ChargeCardAsync(string customerId, string cardToken, decimal amount, string description, CancellationToken ct = default)
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
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        return new CardPaymentResult(
            data.GetProperty("id").GetString()!,
            "Pagamento no cartão aprovado!");
    }

    public async Task<CardTokenizationResult> TokenizeCardAsync(string number, string holderName, string expiryDate, string cvv, string customerId, CancellationToken ct = default)
    {
        var parts = expiryDate.Split('/');
        var body = new
        {
            customer = customerId,
            creditCard = new { holderName, number, expiryMonth = parts[0], expiryYear = parts[1], ccv = cvv },
            creditCardHolderInfo = new { name = holderName, email = "no-reply@sinchrony.com" }
        };

        var resp = await httpClient.PostAsJsonAsync($"{BaseUrl}/creditCards/tokenize", body, ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        return new CardTokenizationResult(
            data.GetProperty("creditCardToken").GetString()!,
            data.GetProperty("creditCardNumber").GetString()!,
            data.GetProperty("creditCardBrand").GetString()!);
    }
}