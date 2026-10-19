using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Sinchrony.Api.Middlewares;

// [Authorize(Roles = ...)] recusado devolvia 403 sem corpo, e o ERP lia isso como "perfil sem
// acesso". Agora todo 403 do pipeline de autorização leva o mesmo formato { error: { code, message } }
// das demais recusas. 401 (não autenticado) segue o comportamento padrão.
public class JsonForbiddenResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { code = "ACCESS_DENIED", message = "Você não tem acesso a este recurso." }
            }));
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
