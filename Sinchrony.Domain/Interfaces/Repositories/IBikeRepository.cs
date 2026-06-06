using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IBikeRepository
{
    Task<Bike?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Bike>> ListByStudioAsync(Guid studioId, CancellationToken ct = default);
    Task AddAsync(Bike bike, CancellationToken ct = default);
    Task RemoveAsync(Bike bike, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}