using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Api.Controllers.Erp;

// Fase 3 do controle de admin/permissões (DEMANDA_CONTROLE_ADMIN_PERMISSOES_FASE3_BACKEND.md).
// A UI já existe no ERP (Cadastro de Usuários → aba Permissões) esperando esses 3 endpoints.
[Authorize(Roles = "admin")]
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ErpPermissionsController(
    IPermissionRepository permissionRepository,
    IPermissionService permissionService,
    IUserRepository userRepository,
    IUnitContext unitContext,
    IAuditService auditService) : ControllerBase
{
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var catalog = await permissionRepository.ListCatalogAsync(ct);
        return Ok(new { data = catalog.Select(p => new { id = p.Id, resource = p.Resource, action = p.Action }) });
    }

    [HttpGet("users/{id}/permissions")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetUserPermissions(Guid id, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null)
            throw DomainException.NotFound("Usuário não encontrado.");

        var granted = await permissionRepository.ListByUserAsync(id, ct);
        return Ok(new { data = granted.Select(p => new { resource = p.Resource, action = p.Action }) });
    }

    [HttpPut("users/{id}/permissions")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> ReplaceUserPermissions(
        Guid id, [FromBody] ReplacePermissionsRequest req, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null)
            throw DomainException.NotFound("Usuário não encontrado.");

        var catalog = (await permissionRepository.ListCatalogAsync(ct)).ToList();
        var catalogLookup = catalog.ToDictionary(p => (p.Resource, p.Action), p => p.Id);

        var requestedIds = new HashSet<Guid>();
        foreach (var item in req.permissions ?? [])
        {
            if (!catalogLookup.TryGetValue((item.resource, item.action), out var permissionId))
                throw DomainException.Validation("INVALID_PERMISSION",
                    $"Permissão inválida: {item.resource}.{item.action}.");
            requestedIds.Add(permissionId);
        }

        // Guarda de segurança: não deixar ninguém se trancar fora acidentalmente removendo o
        // próprio acesso total, nem removendo o último admin com acesso total do sistema.
        var currentIds = (await permissionRepository.ListByUserAsync(id, ct)).Select(p => p.Id).ToHashSet();
        var hadFullAccess = currentIds.Count == catalog.Count;
        var willHaveFullAccess = requestedIds.Count == catalog.Count;

        if (hadFullAccess && !willHaveFullAccess)
        {
            if (id == unitContext.UserId)
                throw new DomainException("CANNOT_REMOVE_OWN_FULL_ACCESS",
                    "Você não pode remover sua própria permissão total.", 403);

            var fullAccessUserIds = await permissionRepository.ListUserIdsWithFullAccessAsync(catalog.Count, ct);
            if (!fullAccessUserIds.Except([id]).Any())
                throw new DomainException("CANNOT_REMOVE_LAST_FULL_ACCESS_ADMIN",
                    "Não é possível remover o acesso total do último administrador com acesso total do sistema.", 403);
        }

        await permissionRepository.ReplaceForUserAsync(id, requestedIds, unitContext.UserId, ct);
        await permissionService.InvalidateUserCacheAsync(id, ct);

        await auditService.LogAsync(
            "user.permissions_replaced", "User", id, unitContext.UserId,
            $"Count: {requestedIds.Count}", ct: ct);

        var updated = await permissionRepository.ListByUserAsync(id, ct);
        return Ok(new { data = updated.Select(p => new { resource = p.Resource, action = p.Action }) });
    }
}

public record PermissionItem(string resource, string action);
public record ReplacePermissionsRequest(List<PermissionItem> permissions);
