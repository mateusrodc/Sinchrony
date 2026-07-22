using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class UnitRepository(ApplicationDbContext db) : IUnitRepository
{
    public async Task<IEnumerable<Unit>> ListAsync(CancellationToken ct = default)
        => await db.Units
            .Include(u => u.Studios)
            .OrderBy(u => u.Name)
            .ToListAsync(ct);

    public async Task<Unit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Units
            .Include(u => u.Studios)
            .Include(u => u.Users)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddAsync(Unit unit, CancellationToken ct = default)
        => await db.Units.AddAsync(unit, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}