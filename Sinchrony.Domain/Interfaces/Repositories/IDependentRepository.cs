using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IDependentRepository
{
    Task<IEnumerable<Dependent>> ListByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<Dependent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Dependent dependent, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}