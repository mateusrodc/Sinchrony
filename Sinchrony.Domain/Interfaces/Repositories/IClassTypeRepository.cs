using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IClassTypeRepository
{
    Task<ClassType?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ClassType>> ListAsync(CancellationToken ct = default);
    Task AddAsync(ClassType classType, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}