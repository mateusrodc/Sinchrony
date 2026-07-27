using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class StudentDetailResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
        name = "Carlos Silva",
        email = "carlos@email.com",
        cpf = "05885186111",
        phone = "63984745681",
        status = "active",
        plan = "Premium",
        credits = 5,
        avatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/a5c10101_1784853105.jpg",
        unitId = "00000000-0000-0000-0000-000000000001",
        unitName = "4Sinchrony Experience",
        isDependent = false,
        responsibleStudentId = (string?)null,
        registeredAt = "2026-01-15T00:00:00Z",
        lastVisit = (string?)null,
        totalClasses = 12,
        cep = "77015012",
        logradouro = "Avenida Teotônio Segurado",
        numero = "1500",
        complemento = (string?)null,
        bairro = "Plano Diretor Sul",
        cidade = "Palmas",
        estado = "TO"
    };
}