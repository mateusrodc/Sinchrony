using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// Demanda "Endereço completo no cliente do Asaas": criação envia endereço/celular, cliente
// existente só é atualizado quando algo difere, e falha no update nunca bloqueia a cobrança.
public class AsaasServiceCustomerTests
{
    private sealed record Call(HttpMethod Method, string Url, string? Body);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new Call(request.Method, request.RequestUri!.ToString(), body));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static AsaasService CreateService(FakeHandler handler) => new(
        new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Asaas:Sandbox"] = "true" }).Build(),
        NullLogger<AsaasService>.Instance);

    private static readonly AsaasCustomerData FullCustomer = new(
        "Maria", "maria@test.com", "12345678901",
        PostalCode: "01310-100", Address: "Av. Paulista", AddressNumber: "1000",
        Complement: "Apto 5", Province: "Bela Vista", MobilePhone: "(11) 99999-8888",
        ExternalReference: "abc");

    private const string EmptySearch = """{"data":[]}""";

    private static string ExistingCustomer(
        string postalCode = "", string address = "", string number = "", string complement = "",
        string province = "", string mobile = "", string externalReference = "") =>
        JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    id = "cus_1", postalCode, address, addressNumber = number, complement, province,
                    mobilePhone = mobile, externalReference
                }
            }
        });

    [Fact]
    public async Task NewCustomer_IsCreatedWithAddressAndPhone()
    {
        var handler = new FakeHandler(req => req.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, EmptySearch)
            : Json(HttpStatusCode.OK, """{"id":"cus_new"}"""));

        var id = await CreateService(handler).GetOrCreateCustomerAsync(FullCustomer);

        id.Should().Be("cus_new");
        var post = handler.Calls.Single(c => c.Method == HttpMethod.Post);
        var body = JsonDocument.Parse(post.Body!).RootElement;
        body.GetProperty("name").GetString().Should().Be("Maria");
        body.GetProperty("cpfCnpj").GetString().Should().Be("12345678901");
        body.GetProperty("postalCode").GetString().Should().Be("01310100");
        body.GetProperty("address").GetString().Should().Be("Av. Paulista");
        body.GetProperty("addressNumber").GetString().Should().Be("1000");
        body.GetProperty("complement").GetString().Should().Be("Apto 5");
        body.GetProperty("province").GetString().Should().Be("Bela Vista");
        body.GetProperty("mobilePhone").GetString().Should().Be("11999998888");
        body.GetProperty("externalReference").GetString().Should().Be("abc");
    }

    [Fact]
    public async Task NewCustomer_WithoutAddress_SendsOnlyNameEmailAndCpf()
    {
        var handler = new FakeHandler(req => req.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, EmptySearch)
            : Json(HttpStatusCode.OK, """{"id":"cus_new"}"""));

        await CreateService(handler).GetOrCreateCustomerAsync(
            new AsaasCustomerData("Maria", "maria@test.com", "12345678901", Complement: "  ", Province: ""));

        var post = handler.Calls.Single(c => c.Method == HttpMethod.Post);
        var props = JsonDocument.Parse(post.Body!).RootElement.EnumerateObject().Select(p => p.Name);
        props.Should().BeEquivalentTo("name", "email", "cpfCnpj");
    }

    [Fact]
    public async Task ExistingCustomerWithoutAddress_IsUpdated()
    {
        var handler = new FakeHandler(req => req.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, ExistingCustomer())
            : Json(HttpStatusCode.OK, """{"id":"cus_1"}"""));

        var id = await CreateService(handler).GetOrCreateCustomerAsync(FullCustomer);

        id.Should().Be("cus_1");
        var put = handler.Calls.Single(c => c.Method == HttpMethod.Put);
        put.Url.Should().EndWith("/customers/cus_1");
        var body = JsonDocument.Parse(put.Body!).RootElement;
        body.GetProperty("postalCode").GetString().Should().Be("01310100");
        body.GetProperty("province").GetString().Should().Be("Bela Vista");
    }

    [Fact]
    public async Task ExistingCustomerAlreadyUpToDate_DoesNotCallUpdate()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, ExistingCustomer(
            postalCode: "01310-100", address: "Av. Paulista", number: "1000", complement: "Apto 5",
            province: "Bela Vista", mobile: "11999998888", externalReference: "abc")));

        await CreateService(handler).GetOrCreateCustomerAsync(FullCustomer);

        handler.Calls.Should().ContainSingle().Which.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task ExistingCustomer_ChangedAddress_UpdatesOnlyDifferingFields()
    {
        var handler = new FakeHandler(req => req.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, ExistingCustomer(
                postalCode: "01310100", address: "Rua Antiga", number: "1000", complement: "Apto 5",
                province: "Bela Vista", mobile: "11999998888", externalReference: "abc"))
            : Json(HttpStatusCode.OK, """{"id":"cus_1"}"""));

        await CreateService(handler).GetOrCreateCustomerAsync(FullCustomer);

        var put = handler.Calls.Single(c => c.Method == HttpMethod.Put);
        var props = JsonDocument.Parse(put.Body!).RootElement.EnumerateObject().ToList();
        props.Should().ContainSingle().Which.Name.Should().Be("address");
    }

    [Fact]
    public async Task ExistingCustomer_UpdateFails_StillReturnsCustomerId()
    {
        var handler = new FakeHandler(req => req.Method == HttpMethod.Get
            ? Json(HttpStatusCode.OK, ExistingCustomer())
            : Json(HttpStatusCode.BadRequest, """{"errors":[{"description":"CEP inválido"}]}"""));

        var id = await CreateService(handler).GetOrCreateCustomerAsync(FullCustomer);

        id.Should().Be("cus_1");
        handler.Calls.Should().Contain(c => c.Method == HttpMethod.Put);
    }
}
