using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface ITeacherUnitRepository
{
    Task<IEnumerable<TeacherUnit>> ListByTeacherAsync(Guid teacherId, CancellationToken ct = default);
    Task UpdateTeacherUnitsAsync(Guid teacherId, List<Guid> unitIds, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}