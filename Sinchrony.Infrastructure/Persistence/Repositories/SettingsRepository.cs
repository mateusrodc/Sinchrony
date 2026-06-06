using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class SettingsRepository(ApplicationDbContext db) : ISettingsRepository
{
    public async Task<Settings?> GetAsync(CancellationToken ct = default)
        => await db.Settings.FirstOrDefaultAsync(ct);

    public async Task AddAsync(Settings settings, CancellationToken ct = default)
        => await db.Settings.AddAsync(settings, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}