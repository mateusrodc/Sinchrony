using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class AdminAlertRepository(ApplicationDbContext db) : IAdminAlertRepository
{
    public async Task<bool> ExistsByTransactionIdAsync(string transactionId, CancellationToken ct = default)
        => await db.AdminAlerts.AnyAsync(a => a.TransactionId == transactionId, ct);

    public async Task AddAsync(AdminAlert alert, CancellationToken ct = default)
        => await db.AdminAlerts.AddAsync(alert, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<AdminAlert?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AdminAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AdminAlertListPage> ListPagedAsync(
        bool? unread, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.AdminAlerts.AsQueryable();
        if (unread == true)
            query = query.Where(a => a.ReadAt == null);

        var total = await query.CountAsync(ct);
        var unreadCount = await db.AdminAlerts.CountAsync(a => a.ReadAt == null, ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AdminAlertListPage(items, total, unreadCount);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
        => await db.AdminAlerts
            .Where(a => a.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ReadAt, DateTime.UtcNow), ct);
}
