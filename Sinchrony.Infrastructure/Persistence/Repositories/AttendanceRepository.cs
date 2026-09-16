using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class AttendanceRepository(ApplicationDbContext db) : IAttendanceRepository
{
    public async Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AttendanceRecords
            .Include(r => r.Student)
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<AttendanceRecord?> GetByClassAndStudentAsync(Guid classId, Guid studentId, CancellationToken ct = default)
        => await db.AttendanceRecords
            .Include(r => r.Student)
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.ClassId == classId && r.StudentId == studentId, ct);

    public async Task<IEnumerable<AttendanceRecord>> ListByClassAsync(Guid classId, CancellationToken ct = default)
        => await db.AttendanceRecords
            .Include(r => r.Student)
            .Include(r => r.Booking)
            .Where(r => r.ClassId == classId)
            .ToListAsync(ct);

    public async Task AddAsync(AttendanceRecord record, CancellationToken ct = default)
        => await db.AttendanceRecords.AddAsync(record, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<IEnumerable<AttendanceRecord>> ListAllAsync(CancellationToken ct = default)
    => await db.AttendanceRecords
        .Include(r => r.Student)
        .Include(r => r.ConfirmedBy)
        .Include(r => r.Booking)
        .Include(r => r.Class)
            .ThenInclude(c => c!.ClassType)
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(ct);

    public async Task<AttendanceRecord?> GetByBookingAsync(
    Guid bookingId, CancellationToken ct = default)
    => await db.AttendanceRecords
        .FirstOrDefaultAsync(a => a.BookingId == bookingId, ct);

    public async Task<IEnumerable<AttendanceRecord>> ListForReportsAsync(
        DateOnly? from, DateOnly? to, IEnumerable<Guid>? studioIds, CancellationToken ct = default)
    {
        var query = db.AttendanceRecords
            .Include(r => r.Student)
            .Include(r => r.ConfirmedBy)
            .Include(r => r.Booking)
            .Include(r => r.Class)
                .ThenInclude(c => c!.ClassType)
            .AsQueryable();

        if (from.HasValue) query = query.Where(r => r.Class != null && r.Class.Date >= from.Value);
        if (to.HasValue) query = query.Where(r => r.Class != null && r.Class.Date <= to.Value);
        if (studioIds is not null) query = query.Where(r => r.Class != null && studioIds.Contains(r.Class.StudioId));

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
    }
}