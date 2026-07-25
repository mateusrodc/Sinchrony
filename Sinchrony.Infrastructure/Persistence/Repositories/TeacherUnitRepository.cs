using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class TeacherUnitRepository(ApplicationDbContext db) : ITeacherUnitRepository
{
    public async Task<IEnumerable<TeacherUnit>> ListByTeacherAsync(
        Guid teacherId, CancellationToken ct = default)
        => await db.TeacherUnits
            .Include(tu => tu.Unit)
            .Where(tu => tu.TeacherId == teacherId)
            .ToListAsync(ct);

    public async Task UpdateTeacherUnitsAsync(
        Guid teacherId, List<Guid> unitIds, CancellationToken ct = default)
    {
        var existing = await db.TeacherUnits
            .Where(tu => tu.TeacherId == teacherId)
            .ToListAsync(ct);

        db.TeacherUnits.RemoveRange(existing);

        foreach (var unitId in unitIds)
            await db.TeacherUnits.AddAsync(TeacherUnit.Create(teacherId, unitId), ct);
    }

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}