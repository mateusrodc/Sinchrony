using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IClassRepository
{
    Task<Class?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Class>> ListAsync(DateOnly? date, string? type, Guid? studioId, CancellationToken ct = default,
        DateOnly? from = null, DateOnly? to = null);
    Task<IEnumerable<Class>> ListTodayAsync(CancellationToken ct = default);
    Task<IEnumerable<Class>> ListByTeacherAsync(Guid teacherId, DateOnly? date, CancellationToken ct = default,
        DateOnly? from = null, DateOnly? to = null);
    Task<int> CountActiveBookingsAsync(Guid classId, CancellationToken ct = default);
    Task AddAsync(Class @class, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<int> CountActiveBookingsWithLockAsync(Guid classId, CancellationToken ct = default);
    Task<(IEnumerable<Class> Items, int Total)> ListPagedAsync(
    DateOnly? date, string? type, Guid? studioId,
    int page, int pageSize, CancellationToken ct = default,
    DateOnly? from = null, DateOnly? to = null);

    // Aulas não canceladas no período (inclusivo) que ocupam a sala OU o professor informados —
    // base da checagem de conflito de horário. Sem rastreamento (só leitura).
    Task<IReadOnlyList<Class>> ListSchedulingCandidatesAsync(
        DateOnly from, DateOnly to, Guid studioId, Guid teacherId, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<Class> classes, CancellationToken ct = default);

    // Usado pelos relatórios ERP (Summary/Occupancy) — filtro por período (from/to) em vez de
    // data única, e teacherId/classTypeId/studioId explícitos (filtro de negócio) combinados com
    // restrictToStudioIds (restrição de unidade do admin logado, sempre aplicada em conjunto).
    Task<IEnumerable<Class>> ListForReportsAsync(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds, CancellationToken ct = default);

    Task<(IEnumerable<Class> Items, int Total)> ListForReportsPagedAsync(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds, int page, int pageSize, CancellationToken ct = default);

    // Relatório de aulas do professor: uma linha por aula que atende aos filtros, com a contagem de
    // reservas/presenças já agregada no banco. Nada é excluído de propósito (aula cancelada, em
    // andamento ou sem presentes entra se o filtro pedir).
    Task<IReadOnlyList<TeacherClassReportRow>> ListForTeacherReportAsync(
        TeacherClassReportFilter filter, CancellationToken ct = default);
}

public record TeacherClassReportFilter(
    DateOnly? From, DateOnly? To, Guid? TeacherId, Guid? ClassTypeId, Guid? StudioId,
    IReadOnlyCollection<Sinchrony.Domain.Enums.ClassStatus>? Statuses, int? MinAttended,
    IEnumerable<Guid>? RestrictToStudioIds);

public record TeacherClassReportRow(
    Guid ClassId, DateOnly Date, string StartTime, Guid TeacherId, string Teacher,
    Guid ClassTypeId, string ClassType, string ClassName, string Studio,
    Sinchrony.Domain.Enums.ClassStatus Status,
    int Booked, int Attended, int NoShow, int CancelledBookings);
