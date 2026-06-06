using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Auth;

public class PermissionsResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        permissions = new[]
        {
            "class:read", "class:book", "class:cancel",
            "purchase:create", "purchase:read",
            "card:manage", "profile:manage",
            "booking:read", "booking:create"
        }
    };
}