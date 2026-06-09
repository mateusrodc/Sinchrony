using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PurchaseRepository(ApplicationDbContext db) : IPurchaseRepository
{
    public async Task<IEnumerable<Purchase>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Purchases
            .Include(p => p.Package)
            .Include(p => p.Coupon)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<Purchase>> ListAllAsync(CancellationToken ct = default)
        => await db.Purchases
            .Include(p => p.Package)
            .Include(p => p.Coupon)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<decimal> TotalRevenueThisMonthAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.Purchases
            .Where(p => p.CreatedAt.Month == now.Month && p.CreatedAt.Year == now.Year)
            .SumAsync(p => p.Amount, ct);
    }

    public async Task AddAsync(Purchase purchase, CancellationToken ct = default)
        => await db.Purchases.AddAsync(purchase, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<(IEnumerable<Purchase> Items, int Total)> ListByUserPagedAsync(
    Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Purchases
            .Include(p => p.Package)
            .Include(p => p.Coupon)
            .Where(p => p.UserId == userId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}