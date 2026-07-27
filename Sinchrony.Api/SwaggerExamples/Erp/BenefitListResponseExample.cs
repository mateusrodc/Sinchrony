using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class BenefitListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "98201f47-1886-4a09-827c-1c4e78527b9c",
                name = "Acesso VIP",
                description = "Acesso antecipado às aulas 48h antes",
                icon = "star",
                active = true
            },
            new
            {
                id = "a1b2c3d4-1234-5678-abcd-ef0123456789",
                name = "Sala Exclusiva",
                description = "Acesso à sala exclusiva de treinos",
                icon = "lock",
                active = true
            }
        }
    };
}