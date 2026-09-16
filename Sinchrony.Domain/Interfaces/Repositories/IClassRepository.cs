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
    Task<(IEnumerable<Class> Items, int Total)> ListPagedAsync(
    DateOnly? date, string? type, Guid? studioId,
    int page, int pageSize, CancellationToken ct = default);

    // Usado pelos relatórios ERP (Summary/Occupancy) — filtro por período (from/to) em vez de
    // data única, e teacherId/classTypeId/studioId explícitos (filtro de negócio) combinados com
    // restrictToStudioIds (restrição de unidade do admin logado, sempre aplicada em conjunto).
    Task<IEnumerable<Class>> ListForReportsAsync(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds, CancellationToken ct = default);

    Task<(IEnumerable<Class> Items, int Total)> ListForReportsPagedAsync(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds, int page, int pageSize, CancellationToken ct = default);
}