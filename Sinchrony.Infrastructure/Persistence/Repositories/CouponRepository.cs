using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class CouponRepository(ApplicationDbContext db) : ICouponRepository
{
    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await db.Coupons.FirstOrDefaultAsync(c => c.Code == code.ToUpper(), ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}