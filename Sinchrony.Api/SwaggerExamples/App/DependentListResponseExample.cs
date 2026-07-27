using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.App;

public class DependentListResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        data = new[]
        {
            new
            {
                id = "58339857-533c-4f1c-9e40-0088717eff67",
                userId = "5c9bcd5b-8ce6-4c26-8a22-35b5febfe813",
                name = "Filho Teste",
                email = "filho.teste@email.com",
                phone = "63999999998",
                birthDate = "2010-05-15",
                cpf = (string?)null,
                canBook = true,
                canCancel = true,
                canViewHistory = true,
                active = true,
                responsibleStudentId = "763b0346-a192-4b7d-ac5f-2bd74f8d20a0",
                createdAt = "2026-07-23T23:20:23Z"
            }
        }
    };
}