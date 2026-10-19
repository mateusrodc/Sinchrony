using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface ITermsAcceptanceRepository
{
    Task AddAsync(TermsAcceptance acceptance, CancellationToken ct = default);
    Task<IReadOnlyList<TermsAcceptance>> ListByUserAsync(Guid userId, CancellationToken ct = default);
}
