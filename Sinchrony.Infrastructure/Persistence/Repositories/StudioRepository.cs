using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class StudioRepository(ApplicationDbContext db) : IStudioRepository
{
    public async Task<Studio?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Studios
            .Include(s => s.Bikes)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IEnumerable<Studio>> ListAsync(CancellationToken ct = default)
        => await db.Studios.OrderBy(s => s.Name).ToListAsync(ct);

    public async Task AddAsync(Studio studio, CancellationToken ct = default)
        => await db.Studios.AddAsync(studio, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}