using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class WaitlistRepository(ApplicationDbContext db) : IWaitlistRepository
{
    public async Task<WaitlistEntry?> GetByClassAndStudentAsync(
        Guid classId, Guid studentId, CancellationToken ct = default)
        => await db.WaitlistEntries
            .FirstOrDefaultAsync(w => w.ClassId == classId && w.StudentId == studentId, ct);

    public async Task<IEnumerable<WaitlistEntry>> ListByClassAsync(
        Guid classId, CancellationToken ct = default)
        => await db.WaitlistEntries
            .Include(w => w.Student)
            .Where(w => w.ClassId == classId && w.Status == "waiting")
            .OrderBy(w => w.Position)
            .ToListAsync(ct);

    public async Task<WaitlistEntry?> GetNextWaitingAsync(
        Guid classId, CancellationToken ct = default)
        => await db.WaitlistEntries
            .Include(w => w.Student)
            .Where(w => w.ClassId == classId && w.Status == "waiting")
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync(ct);

    public async Task<WaitlistEntry?> GetCurrentNotifiedAsync(
        Guid classId, CancellationToken ct = default)
        => await db.WaitlistEntries
            .Where(w => w.ClassId == classId && w.Status == "notified")
            .FirstOrDefaultAsync(ct);

    public async Task<int> CountByClassAsync(Guid classId, CancellationToken ct = default)
        => await db.WaitlistEntries
            .CountAsync(w => w.ClassId == classId && w.Status == "waiting", ct);

    public async Task AddAsync(WaitlistEntry entry, CancellationToken ct = default)
        => await db.WaitlistEntries.AddAsync(entry, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}