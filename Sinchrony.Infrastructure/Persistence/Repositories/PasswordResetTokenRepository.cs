using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository(ApplicationDbContext db) : IPasswordResetTokenRepository
{
    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
        => await db.PasswordResetTokens.AddAsync(token, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}