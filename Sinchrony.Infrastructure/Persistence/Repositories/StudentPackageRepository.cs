using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class StudentPackageRepository(ApplicationDbContext db) : IStudentPackageRepository
{
    public async Task<StudentPackage?> GetActiveByStudentAsync(Guid studentId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .Include(sp => sp.Allocations)
            .FirstOrDefaultAsync(sp =>
                sp.StudentId == studentId &&
                sp.Status == StudentPackageStatus.active, ct);

    public async Task<StudentPackage?> GetQueuedByStudentAsync(Guid studentId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package)
            .FirstOrDefaultAsync(sp =>
                sp.StudentId == studentId &&
                sp.Status == StudentPackageStatus.queued, ct);

    public async Task<IEnumerable<StudentPackage>> ListByStudentAsync(Guid studentId, CancellationToken ct = default)
        => await db.StudentPackages
            .Include(sp => sp.Package).ThenInclude(p => p!.PackageType)
            .Where(sp => sp.StudentId == studentId)
            .OrderByDescending(sp => sp.PurchasedAt)
            .ToListAsync(ct);

    public async Task AddAsync(StudentPackage studentPackage, CancellationToken ct = default)
        => await db.StudentPackages.AddAsync(studentPackage, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}