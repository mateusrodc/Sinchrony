using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Api.Filters;

// Camada ADICIONAL de permissão granular, só usada em endpoints novos — roda depois do
// [Authorize(Roles=...)] de sempre (que já garante usuário autenticado antes deste filtro
// executar). Implementado como IAsyncActionFilter, não IAuthorizationHandler: não existe
// nenhuma infra de Policy/AddAuthorization no projeto hoje, e um IAuthorizationHandler
// exigiria um IAuthorizationPolicyProvider customizado só pra suportar políticas dinâmicas
// por (resource, action) — complexidade desnecessária pra um atributo de dois parâmetros.
// Lança DomainException em vez de setar context.Result diretamente pra reaproveitar o mesmo
// ExceptionMiddleware que já renderiza {"error":{"code","message"}} pra todo o resto da API.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute(string resource, string action) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();

        var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            throw DomainException.Unauthorized("Usuário não autenticado.");

        var hasPermission = await permissionService.HasPermissionAsync(
            userId, resource, action, context.HttpContext.RequestAborted);

        if (!hasPermission)
            throw new DomainException("PERMISSION_DENIED",
                "Você não tem permissão para executar esta ação.", 403);

        await next();
    }
}
