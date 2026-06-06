using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class BikeRepository(ApplicationDbContext db) : IBikeRepository
{
    public async Task<Bike?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Bikes.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IEnumerable<Bike>> ListByStudioAsync(Guid studioId, CancellationToken ct = default)
        => await db.Bikes
            .Where(b => b.StudioId == studioId)
            .OrderBy(b => b.Number)
            .ToListAsync(ct);

    public async Task AddAsync(Bike bike, CancellationToken ct = default)
        => await db.Bikes.AddAsync(bike, ct);

    public Task RemoveAsync(Bike bike, CancellationToken ct = default)
    {
        db.Bikes.Remove(bike);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}