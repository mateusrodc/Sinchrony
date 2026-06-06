using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.SwaggerExamples.Erp;

public class StudentDetailResponseExample : IExamplesProvider<object>
{
    public object GetExamples() => new
    {
        id = "a5c10101-5aa0-47a0-ab3d-6189ecec2a99",
        name = "Carlos Silva",
        email = "carlos@email.com",
        phone = "63984745681",
        status = "active",
        plan = (string?)null,
        credits = 5,
        registeredAt = "2026-01-15",
        lastVisit = (string?)null,
        totalClasses = 0
    };
}