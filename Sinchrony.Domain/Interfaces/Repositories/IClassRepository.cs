using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IClassRepository
{
    Task<Class?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Class>> ListAsync(DateOnly? date, string? type, Guid? studioId, CancellationToken ct = default);
    Task<IEnumerable<Class>> ListTodayAsync(CancellationToken ct = default);
    Task<IEnumerable<Class>> ListByTeacherAsync(Guid teacherId, DateOnly? date, CancellationToken ct = default);
    Task<int> CountActiveBookingsAsync(Guid classId, CancellationToken ct = default);
    Task AddAsync(Class @class, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<int> CountActiveBookingsWithLockAsync(Guid classId, CancellationToken ct = default);
}