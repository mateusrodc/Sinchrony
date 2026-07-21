using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PackageTypeRepository(ApplicationDbContext db) : IPackageTypeRepository
{
    public async Task<IEnumerable<PackageType>> ListAsync(CancellationToken ct = default)
        => await db.PackageTypes.OrderBy(p => p.Rank).ThenBy(p => p.Name).ToListAsync(ct);

    public async Task<PackageType?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PackageTypes.Include(p => p.Packages).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(PackageType packageType, CancellationToken ct = default)
        => await db.PackageTypes.AddAsync(packageType, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}