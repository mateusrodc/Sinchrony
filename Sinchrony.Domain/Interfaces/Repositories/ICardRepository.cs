using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Card>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    // Cartão padrão de cada aluno na lista — usado pra exibir bandeira/final sem N+1 queries
    // (GET /api/subscriptions lista várias assinaturas de uma vez).
    Task<IEnumerable<Card>> ListDefaultByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task<bool> ExistsByTokenAsync(Guid userId, string token, CancellationToken ct = default);
    Task<Card?> GetByTokenAsync(Guid userId, string token, CancellationToken ct = default);
    Task AddAsync(Card card, CancellationToken ct = default);
    Task RemoveAsync(Card card, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}