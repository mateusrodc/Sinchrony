using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class UnitListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "00000000-0000-0000-0000-000000000001",
                name = "4Sinchrony Experience",
                address = "Av. JK, Quadra 103 Sul",
                phone = "(63) 99999-0000",
                email = "contato@4sinchrony.com.br",
                active = true,
                studiosCount = 3,
                createdAt = "2026-06-01T00:00:00Z",
                cep = "77015012",
                logradouro = "Avenida JK",
                numero = "103",
                complemento = (string?)null,
                bairro = "Plano Diretor Sul",
                cidade = "Palmas",
                estado = "TO"
            }
        }
    };
}