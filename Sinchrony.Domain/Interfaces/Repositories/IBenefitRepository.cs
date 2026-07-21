using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IBenefitRepository
{
    Task<IEnumerable<Benefit>> ListAsync(CancellationToken ct = default);
    Task<Benefit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Benefit benefit, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}