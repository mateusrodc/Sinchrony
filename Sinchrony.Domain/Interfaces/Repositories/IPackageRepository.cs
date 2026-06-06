using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IPackageRepository
{
    Task<Package?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Package>> ListAsync(bool? activeOnly, CancellationToken ct = default);
    Task AddAsync(Package package, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}