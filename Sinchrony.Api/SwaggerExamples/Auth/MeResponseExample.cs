using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Auth;

public class MeResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
        name = "Mateus Rodrigues",
        email = "mateus@email.com",
        role = "student",
        credits = 10,
        phone = "63984745681",
        avatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/a5c10101_1784853105.jpg",
        cpf = "05885186111",
        cep = "77015012",
        logradouro = "Avenida Teotônio Segurado",
        numero = "1500",
        complemento = (string?)null,
        bairro = "Plano Diretor Sul",
        cidade = "Palmas",
        estado = "TO",
        plan = "Premium",
        isGlobalAdmin = false,
        unitId = "00000000-0000-0000-0000-000000000001"
    };
}