using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IUnitRepository
{
    Task<IEnumerable<Unit>> ListAsync(CancellationToken ct = default);
    Task<Unit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Unit unit, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}