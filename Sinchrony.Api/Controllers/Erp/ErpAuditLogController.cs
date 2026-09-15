using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.Filters;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

// Fase 2 — DEMANDA_CONTROLE_ADMIN_PERMISSOES_BACKEND.md. O log (AuditLog/IAuditService) já
// existe e é escrito em ~10 pontos do sistema; até este controller não existia nenhuma forma
// de consultá-lo — era uma gaveta que só recebia, nunca abria.
[Authorize(Roles = "admin")]
[ApiController]
[Route("api/audit-log")]
[Produces("application/json")]
public class ErpAuditLogController(IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpGet]
    [RequirePermission("audit_log", "view")]
    public async Task<IActionResult> List(
        [FromQuery] string? entity,
        [FromQuery] Guid? userId,
        [FromQuery] string? @event,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var (items, total) = await auditLogRepository.ListAsync(
            entity, userId, @event, from, to, page, pageSize, ct);

        return Ok(PagedResult.Create(items.Select(a => new
        {
            id = a.Id,
            @event = a.Event,
            entity = a.Entity,
            entityId = a.EntityId,
            userId = a.UserId,
            details = a.Details,
            ipAddress = a.IpAddress,
            createdAt = a.CreatedAt
        }), page, pageSize, total));
    }
}
