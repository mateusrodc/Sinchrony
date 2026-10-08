using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class ClassRepository(ApplicationDbContext db) : IClassRepository
{
    public async Task<Class?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<Class>> ListAsync(DateOnly? date, string? type, Guid? studioId, CancellationToken ct = default,
        DateOnly? from = null, DateOnly? to = null)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .AsQueryable();

        if (date.HasValue) query = query.Where(c => c.Date == date.Value);
        if (from.HasValue) query = query.Where(c => c.Date >= from.Value);
        if (to.HasValue) query = query.Where(c => c.Date <= to.Value);
        if (!string.IsNullOrEmpty(type)) query = query.Where(c => c.ClassType!.Name.ToLower() == type.ToLower());
        if (studioId.HasValue) query = query.Where(c => c.StudioId == studioId.Value);

        return await query.OrderBy(c => c.Date).ThenBy(c => c.StartTime).ToListAsync(ct);
    }

    public async Task<IEnumerable<Class>> ListTodayAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .Where(c => c.Date == today)
            .OrderBy(c => c.StartTime)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Class>> ListByTeacherAsync(
        Guid teacherId, DateOnly? date, CancellationToken ct = default,
        DateOnly? from = null, DateOnly? to = null)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .Where(c => c.TeacherId == teacherId);

        if (date.HasValue) query = query.Where(c => c.Date == date.Value);
        if (from.HasValue) query = query.Where(c => c.Date >= from.Value);
        if (to.HasValue) query = query.Where(c => c.Date <= to.Value);

        return await query.OrderBy(c => c.Date).ThenBy(c => c.StartTime).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Class>> ListSchedulingCandidatesAsync(
        DateOnly from, DateOnly to, Guid studioId, Guid teacherId, CancellationToken ct = default)
        => await db.Classes.AsNoTracking()
            .Where(c => c.Date >= from && c.Date <= to
                && c.Status != ClassStatus.cancelled
                && (c.StudioId == studioId || c.TeacherId == teacherId))
            .ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<Class> classes, CancellationToken ct = default)
        => await db.Classes.AddRangeAsync(classes, ct);

    public async Task<int> CountActiveBookingsAsync(Guid classId, CancellationToken ct = default)
        => await db.Bookings.CountAsync(b => b.ClassId == classId && b.Status != BookingStatus.cancelled, ct);

    public async Task AddAsync(Class @class, CancellationToken ct = default)
        => await db.Classes.AddAsync(@class, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<int> CountActiveBookingsWithLockAsync(Guid classId, CancellationToken ct = default)
    {
        // SELECT FOR UPDATE no PostgreSQL via raw SQL — garante lock pessimista
        return await db.Bookings
            .FromSqlRaw(
                @"SELECT b.* FROM bookings b 
              WHERE b.""ClassId"" = {0} 
              AND b.""Status"" != 'cancelled'
              FOR UPDATE SKIP LOCKED",
                classId)
            .CountAsync(ct);
    }
    public async Task<(IEnumerable<Class> Items, int Total)> ListPagedAsync(
    DateOnly? date, string? type, Guid? studioId,
    int page, int pageSize, CancellationToken ct = default,
    DateOnly? from = null, DateOnly? to = null)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .AsQueryable();

        if (date.HasValue) query = query.Where(c => c.Date == date.Value);
        if (from.HasValue) query = query.Where(c => c.Date >= from.Value);
        if (to.HasValue) query = query.Where(c => c.Date <= to.Value);
        if (!string.IsNullOrEmpty(type)) query = query.Where(c => c.ClassType!.Name.ToLower() == type.ToLower());
        if (studioId.HasValue) query = query.Where(c => c.StudioId == studioId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Date).ThenBy(c => c.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    private IQueryable<Class> BuildReportsQuery(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .AsQueryable();

        if (from.HasValue) query = query.Where(c => c.Date >= from.Value);
        if (to.HasValue) query = query.Where(c => c.Date <= to.Value);
        if (studioId.HasValue) query = query.Where(c => c.StudioId == studioId.Value);
        if (teacherId.HasValue) query = query.Where(c => c.TeacherId == teacherId.Value);
        if (classTypeId.HasValue) query = query.Where(c => c.ClassTypeId == classTypeId.Value);
        if (restrictToStudioIds is not null) query = query.Where(c => restrictToStudioIds.Contains(c.StudioId));

        return query;
    }

    public async Task<IEnumerable<Class>> ListForReportsAsync(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds, CancellationToken ct = default)
    {
        var query = BuildReportsQuery(from, to, studioId, teacherId, classTypeId, restrictToStudioIds);
        return await query.OrderBy(c => c.Date).ThenBy(c => c.StartTime).ToListAsync(ct);
    }

    public async Task<(IEnumerable<Class> Items, int Total)> ListForReportsPagedAsync(
        DateOnly? from, DateOnly? to, Guid? studioId, Guid? teacherId, Guid? classTypeId,
        IEnumerable<Guid>? restrictToStudioIds, int page, int pageSize, CancellationToken ct = default)
    {
        var query = BuildReportsQuery(from, to, studioId, teacherId, classTypeId, restrictToStudioIds);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.Date).ThenBy(c => c.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<TeacherClassReportRow>> ListForTeacherReportAsync(
        TeacherClassReportFilter filter, CancellationToken ct = default)
    {
        var query = db.Classes.AsNoTracking().AsQueryable();

        if (filter.From.HasValue) query = query.Where(c => c.Date >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(c => c.Date <= filter.To.Value);
        if (filter.TeacherId.HasValue) query = query.Where(c => c.TeacherId == filter.TeacherId.Value);
        if (filter.ClassTypeId.HasValue) query = query.Where(c => c.ClassTypeId == filter.ClassTypeId.Value);
        if (filter.StudioId.HasValue) query = query.Where(c => c.StudioId == filter.StudioId.Value);
        if (filter.Statuses is { Count: > 0 }) query = query.Where(c => filter.Statuses.Contains(c.Status));
        if (filter.RestrictToStudioIds is not null)
            query = query.Where(c => filter.RestrictToStudioIds.Contains(c.StudioId));

        // Contagens agregadas no banco (uma query), sem carregar reservas/presenças. "Presente" é a
        // presença confirmada (AttendanceRecord.Status == attended) pelo professor ou pela recepção.
        var rows = await query
            .OrderBy(c => c.Date).ThenBy(c => c.StartTime).ThenBy(c => c.Name)
            .Select(c => new TeacherClassReportRow(
                c.Id, c.Date, c.StartTime, c.TeacherId,
                c.Teacher != null ? c.Teacher.Name : string.Empty,
                c.ClassTypeId,
                c.ClassType != null ? c.ClassType.Name : string.Empty,
                c.Name,
                c.Studio != null ? c.Studio.Name : string.Empty,
                c.Status,
                c.Bookings.Count(b => b.Status != BookingStatus.cancelled && b.Status != BookingStatus.waitlisted),
                db.AttendanceRecords.Count(a => a.ClassId == c.Id && a.Status == BookingStatus.attended),
                db.AttendanceRecords.Count(a => a.ClassId == c.Id && a.Status == BookingStatus.no_show),
                c.Bookings.Count(b => b.Status == BookingStatus.cancelled)))
            .ToListAsync(ct);

        return filter.MinAttended.HasValue
            ? rows.Where(r => r.Attended >= filter.MinAttended.Value).ToList()
            : rows;
    }
}
