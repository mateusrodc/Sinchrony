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
        avatar = (string?)null
    };
}