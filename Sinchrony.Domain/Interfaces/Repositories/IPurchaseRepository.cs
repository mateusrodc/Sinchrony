using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IPurchaseRepository
{
    Task<IEnumerable<Purchase>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Purchase>> ListAllAsync(CancellationToken ct = default);
    Task<decimal> TotalRevenueThisMonthAsync(CancellationToken ct = default);
    Task AddAsync(Purchase purchase, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}