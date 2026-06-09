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

    public async Task<IEnumerable<Class>> ListAsync(DateOnly? date, string? type, Guid? studioId, CancellationToken ct = default)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .AsQueryable();

        if (date.HasValue) query = query.Where(c => c.Date == date.Value);
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

    public async Task<IEnumerable<Class>> ListByTeacherAsync(Guid teacherId, DateOnly? date, CancellationToken ct = default)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .Where(c => c.TeacherId == teacherId);

        if (date.HasValue) query = query.Where(c => c.Date == date.Value);
        return await query.OrderBy(c => c.Date).ThenBy(c => c.StartTime).ToListAsync(ct);
    }

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
    int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Classes
            .Include(c => c.ClassType)
            .Include(c => c.Teacher)
            .Include(c => c.Studio)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .AsQueryable();

        if (date.HasValue) query = query.Where(c => c.Date == date.Value);
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
}