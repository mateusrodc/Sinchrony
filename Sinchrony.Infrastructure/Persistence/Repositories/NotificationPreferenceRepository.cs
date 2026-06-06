using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class NotificationPreferenceRepository(ApplicationDbContext db) : INotificationPreferenceRepository
{
    public async Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await db.NotificationPreferences.FirstOrDefaultAsync(n => n.UserId == userId, ct);

    public async Task AddAsync(NotificationPreference preference, CancellationToken ct = default)
        => await db.NotificationPreferences.AddAsync(preference, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}