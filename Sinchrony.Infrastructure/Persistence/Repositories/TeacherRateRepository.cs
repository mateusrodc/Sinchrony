using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class TeacherRateRepository(ApplicationDbContext db) : ITeacherRateRepository
{
    public async Task<IReadOnlyList<ClassRate>> ListClassRatesAsync(CancellationToken ct = default)
        => await db.ClassRates
            .Include(r => r.ClassType)
            .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TeacherBonusRate>> ListBonusRatesAsync(CancellationToken ct = default)
        => await db.TeacherBonusRates
            .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task AddClassRateAsync(ClassRate rate, CancellationToken ct = default)
        => await db.ClassRates.AddAsync(rate, ct);

    public async Task AddBonusRateAsync(TeacherBonusRate rate, CancellationToken ct = default)
        => await db.TeacherBonusRates.AddAsync(rate, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
