using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IStudioRepository
{
    Task<Studio?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Studio>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Studio studio, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}