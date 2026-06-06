using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IReferralRepository
{
    Task<IEnumerable<Referral>> ListByReferrerAsync(Guid referrerId, CancellationToken ct = default);
    Task AddAsync(Referral referral, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}