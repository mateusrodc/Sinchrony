using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class DependentPackageAllocationRepository(ApplicationDbContext db) : IDependentPackageAllocationRepository
{
    public async Task<DependentPackageAllocation?> GetByStudentPackageAndDependentAsync(
        Guid studentPackageId, Guid? dependentId, CancellationToken ct = default)
        => await db.DependentPackageAllocations
            .FirstOrDefaultAsync(a =>
                a.StudentPackageId == studentPackageId &&
                a.DependentId == dependentId, ct);

    public async Task AddAsync(DependentPackageAllocation allocation, CancellationToken ct = default)
        => await db.DependentPackageAllocations.AddAsync(allocation, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}