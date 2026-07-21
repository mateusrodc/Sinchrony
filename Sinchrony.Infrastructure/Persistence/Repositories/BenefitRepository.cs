using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class BenefitRepository(ApplicationDbContext db) : IBenefitRepository
{
    public async Task<IEnumerable<Benefit>> ListAsync(CancellationToken ct = default)
        => await db.Benefits.Where(b => b.Active).OrderBy(b => b.Name).ToListAsync(ct);

    public async Task<Benefit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Benefits.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task AddAsync(Benefit benefit, CancellationToken ct = default)
        => await db.Benefits.AddAsync(benefit, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}