using System.Net;
using System.Text;

namespace Sinchrony.Api.Middlewares;

public class SwaggerBasicAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/docs") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                !authHeader.ToString().StartsWith("Basic "))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"4Sinchrony API Docs\"";
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            var credentials = Encoding.UTF8
                .GetString(Convert.FromBase64String(
                    authHeader.ToString()["Basic ".Length..].Trim()))
                .Split(':', 2);

            var username = credentials[0];
            var password = credentials.Length > 1 ? credentials[1] : string.Empty;

            var expectedUser = configuration["Swagger:Username"] ?? "sinchrony";
            var expectedPass = configuration["Swagger:Password"] ?? "sinchrony@2026";

            if (username != expectedUser || password != expectedPass)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"4Sinchrony API Docs\"";
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }

        await next(context);
    }
}