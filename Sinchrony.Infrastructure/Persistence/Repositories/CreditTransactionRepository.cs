using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class CreditTransactionRepository(ApplicationDbContext db) : ICreditTransactionRepository
{
    public async Task<IEnumerable<CreditTransaction>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.CreditTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(CreditTransaction transaction, CancellationToken ct = default)
        => await db.CreditTransactions.AddAsync(transaction, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}