using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);

    // Fase 2 — DEMANDA_CONTROLE_ADMIN_PERMISSOES_BACKEND.md. O log já é escrito em ~10 pontos
    // do sistema desde antes desta fase; até agora não existia nenhuma forma de consultá-lo.
    Task<(IEnumerable<AuditLog> Items, int Total)> ListAsync(
        string? entity, Guid? userId, string? @event,
        DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default);
}