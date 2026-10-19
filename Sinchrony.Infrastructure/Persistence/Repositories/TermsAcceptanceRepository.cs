using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class TermsAcceptanceRepository(ApplicationDbContext db) : ITermsAcceptanceRepository
{
    public async Task AddAsync(TermsAcceptance acceptance, CancellationToken ct = default)
        => await db.TermsAcceptances.AddAsync(acceptance, ct);

    public async Task<IReadOnlyList<TermsAcceptance>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.TermsAcceptances
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.AcceptedAt)
            .ToListAsync(ct);
}
