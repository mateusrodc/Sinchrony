using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class CardRepository(ApplicationDbContext db) : ICardRepository
{
    public async Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Cards.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<Card>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Cards
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> ExistsByTokenAsync(Guid userId, string token, CancellationToken ct = default)
        => await db.Cards.AnyAsync(c => c.UserId == userId && c.Token == token, ct);

    public async Task AddAsync(Card card, CancellationToken ct = default)
        => await db.Cards.AddAsync(card, ct);

    public Task RemoveAsync(Card card, CancellationToken ct = default)
    {
        db.Cards.Remove(card);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}