using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class ReferralRepository(ApplicationDbContext db) : IReferralRepository
{
    public async Task<IEnumerable<Referral>> ListByReferrerAsync(Guid referrerId, CancellationToken ct = default)
        => await db.Referrals
            .Include(r => r.Referred)
            .Where(r => r.ReferrerId == referrerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Referral referral, CancellationToken ct = default)
        => await db.Referrals.AddAsync(referral, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}