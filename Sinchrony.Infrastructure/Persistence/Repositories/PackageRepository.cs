using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PackageRepository(ApplicationDbContext db) : IPackageRepository
{
    public async Task<Package?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Packages.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Package>> ListAsync(bool? activeOnly, CancellationToken ct = default)
    {
        var query = db.Packages.AsQueryable();
        if (activeOnly == true) query = query.Where(p => p.Active);
        return await query.OrderBy(p => p.DisplayOrder).ToListAsync(ct);
    }

    public async Task AddAsync(Package package, CancellationToken ct = default)
        => await db.Packages.AddAsync(package, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}