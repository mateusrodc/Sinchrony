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
        .Include(r => r.Class)
        .Include(r => r.ConfirmedBy)
        .Include(r => r.Booking)
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync(ct);

    public async Task<AttendanceRecord?> GetByBookingAsync(
    Guid bookingId, CancellationToken ct = default)
    => await db.AttendanceRecords
        .FirstOrDefaultAsync(a => a.BookingId == bookingId, ct);
}