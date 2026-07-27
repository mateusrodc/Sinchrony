using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class TeacherListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "2fa85f64-5717-4562-b3fc-2c963f66afa6",
                name = "Ádria Silva",
                email = "adria@sinchrony.com",
                cpf = "12345678909",
                phone = "(63) 98888-0000",
                active = true,
                avatar = "https://wqswkpblilxaubdoswbc.supabase.co/storage/v1/object/public/sinchrony-avatars/avatars/2fa85f64_1784827850.jpg",
                unitIds = new[] { "00000000-0000-0000-0000-000000000001" },
                units = new[] { new { id = "00000000-0000-0000-0000-000000000001", name = "4Sinchrony Experience" } },
                specialties = new[] { "Bike", "Pilates" },
                cep = "77015012",
                logradouro = "Avenida Teotônio Segurado",
                numero = "200",
                complemento = (string?)null,
                bairro = "Plano Diretor Sul",
                cidade = "Palmas",
                estado = "TO"
            }
        }
    };
}