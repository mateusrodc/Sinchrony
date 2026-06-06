using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class SessionRepository(ApplicationDbContext db) : ISessionRepository
{
    public async Task<ClassSession?> GetByClassIdAsync(Guid classId, CancellationToken ct = default)
        => await db.ClassSessions
            .Include(s => s.Class)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(s => s.ClassId == classId, ct);

    public async Task AddAsync(ClassSession session, CancellationToken ct = default)
        => await db.ClassSessions.AddAsync(session, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}