using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IPackageTypeRepository
{
    Task<IEnumerable<PackageType>> ListAsync(CancellationToken ct = default);
    Task<PackageType?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(PackageType packageType, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}