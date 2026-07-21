using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IDependentPackageAllocationRepository
{
    Task<DependentPackageAllocation?> GetByStudentPackageAndDependentAsync(
        Guid studentPackageId, Guid? dependentId, CancellationToken ct = default);
    Task AddAsync(DependentPackageAllocation allocation, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}