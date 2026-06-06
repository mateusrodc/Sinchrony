using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class ErpAuthValidateResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        user = new
        {
            id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
            name = "Admin",
            email = "admin@sinchrony.com",
            role = "admin"
        }
    };
}