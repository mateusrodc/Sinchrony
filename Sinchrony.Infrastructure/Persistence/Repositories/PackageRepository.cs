using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PackageRepository(ApplicationDbContext db) : IPackageRepository
{
    public async Task<Package?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => await db.Packages
        .Include(p => p.PackageType)
        .Include(p => p.PackageBenefits).ThenInclude(pb => pb.Benefit)
        .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Package>> ListAsync(bool? activeOnly, CancellationToken ct = default)
    => await db.Packages
        .Include(p => p.PackageType)
        .Include(p => p.PackageBenefits).ThenInclude(pb => pb.Benefit)
        .Where(p => activeOnly == null || p.Active == activeOnly)
        .OrderBy(p => p.DisplayOrder)
        .ToListAsync(ct);

    public async Task AddAsync(Package package, CancellationToken ct = default)
        => await db.Packages.AddAsync(package, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task UpdateBenefitsAsync(Guid packageId, List<Guid> benefitIds, CancellationToken ct = default)
    {
        var existing = await db.PackageBenefits
            .Where(pb => pb.PackageId == packageId)
            .ToListAsync(ct);

        db.PackageBenefits.RemoveRange(existing);

        foreach (var benefitId in benefitIds)
            await db.PackageBenefits.AddAsync(PackageBenefit.Create(packageId, benefitId), ct);
    }
}