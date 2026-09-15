using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class AuditLogRepository(ApplicationDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
        => await db.AuditLogs.AddAsync(log, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<(IEnumerable<AuditLog> Items, int Total)> ListAsync(
        string? entity, Guid? userId, string? @event,
        DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(entity))
            query = query.Where(a => a.Entity == entity);
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrEmpty(@event))
            query = query.Where(a => a.Event == @event);
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
