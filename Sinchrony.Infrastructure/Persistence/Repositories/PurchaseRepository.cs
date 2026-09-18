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

    public async Task<PurchaseListPage> ListErpPagedAsync(
        PurchaseListFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Purchases.AsNoTracking().AsQueryable();

        if (filter.From.HasValue)
            query = query.Where(p => p.CreatedAt >= filter.From.Value);
        if (filter.ToExclusive.HasValue)
            query = query.Where(p => p.CreatedAt < filter.ToExclusive.Value);
        if (!string.IsNullOrEmpty(filter.Status))
            query = query.Where(p => p.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.PaymentMethod))
            query = query.Where(p => p.PaymentMethod == filter.PaymentMethod);
        if (filter.UnitId.HasValue)
            query = query.Where(p => p.User!.UnitId == filter.UnitId.Value);
        if (!string.IsNullOrWhiteSpace(filter.StudentSearch))
        {
            // Escapa curingas do LIKE para o termo digitado ser tratado como texto literal
            var term = filter.StudentSearch.Trim()
                .Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{term}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.User!.Name, pattern) ||
                EF.Functions.ILike(p.User.Email, pattern));
        }

        var total = await query.CountAsync(ct);
        var totalAmount = await query.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var confirmedAmount = await query.Where(p => p.Status == "confirmed")
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var items = await query
            .Include(p => p.User)
            .Include(p => p.Package)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PurchaseListPage(items, total, totalAmount, confirmedAmount);
    }

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