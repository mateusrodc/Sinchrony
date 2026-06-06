using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Auth;

public class LoginResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        access_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        refresh_token = "2E54304158CD88CCC232A5A3DD3C883E939E72E196CA1C8D6A33D935B99F73C9",
        token_type = "Bearer",
        expires_in = 900,
        user = new
        {
            id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
            name = "Mateus Rodrigues",
            email = "mateus@email.com",
            role = "student",
            credits = 10,
            phone = "63984745681",
            avatar = (string?)null
        }
    };
}