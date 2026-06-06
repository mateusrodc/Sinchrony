using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface ICreditTransactionRepository
{
    Task<IEnumerable<CreditTransaction>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(CreditTransaction transaction, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}