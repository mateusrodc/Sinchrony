using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

// Avisos pro admin quando uma assinatura recorrente vence (ESPECIFICACAO_API_STATUS_ASSINATURAS.md
// §4.7). "Lido" é global — um admin marcando como lido vale pra todos, mesmo padrão do resto do
// sistema pra um estúdio só.
[Authorize(Roles = "admin")]
[ApiController]
[Route("api/alerts")]
[Produces("application/json")]
public class AdminAlertsController(IAdminAlertRepository adminAlertRepository) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> List(
        [FromQuery] bool? unread,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var result = await adminAlertRepository.ListPagedAsync(unread, page, pageSize, ct);

        var data = result.Items.Select(a => new
        {
            id = a.Id,
            type = a.Type,
            studentId = a.StudentId,
            studentPackageId = a.StudentPackageId,
            transactionId = a.TransactionId,
            amount = a.Amount,
            createdAt = a.CreatedAt,
            readAt = a.ReadAt
        });

        var paged = PagedResult.Create(data, page, pageSize, result.Total);
        return Ok(new { paged.Data, paged.Pagination, unreadCount = result.UnreadCount });
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var alert = await adminAlertRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Alerta não encontrado.");

        alert.MarkRead();
        await adminAlertRepository.SaveAsync(ct);

        return Ok(new { id = alert.Id, readAt = alert.ReadAt });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await adminAlertRepository.MarkAllReadAsync(ct);
        return Ok(new { message = "Todos os alertas foram marcados como lidos." });
    }
}
