using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class BookingRepository(ApplicationDbContext db) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Bookings
            .Include(b => b.Class).ThenInclude(c => c!.Teacher)
            .Include(b => b.Class).ThenInclude(c => c!.Studio)
            .Include(b => b.Student)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IEnumerable<Booking>> ListByStudentAsync(Guid studentId, string? status, bool history, CancellationToken ct = default)
    {
        var query = db.Bookings
            .Include(b => b.Class).ThenInclude(c => c!.ClassType)
            .Include(b => b.Class).ThenInclude(c => c!.Studio)
            .Where(b => b.StudentId == studentId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(b => b.Status.ToString() == status);

        if (!history)
            query = query.Where(b => b.Status == BookingStatus.confirmed);

        return await query.OrderByDescending(b => b.BookedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<Booking>> ListErpAsync(Guid? classId, Guid? studentId, string? status, CancellationToken ct = default)
    {
        var query = db.Bookings
            .Include(b => b.Class)
            .Include(b => b.Student)
            .AsQueryable();

        if (classId.HasValue) query = query.Where(b => b.ClassId == classId.Value);
        if (studentId.HasValue) query = query.Where(b => b.StudentId == studentId.Value);
        if (!string.IsNullOrEmpty(status)) query = query.Where(b => b.Status.ToString() == status);

        return await query.OrderByDescending(b => b.BookedAt).ToListAsync(ct);
    }

    public async Task<bool> HasActiveBookingAsync(Guid studentId, Guid classId, CancellationToken ct = default)
        => await db.Bookings.AnyAsync(b =>
            b.StudentId == studentId && b.ClassId == classId && b.Status != BookingStatus.cancelled, ct);

    public async Task<bool> HasTimeConflictAsync(
    Guid studentId, DateOnly date, string startTime, string endTime,
    Guid? excludeClassId, CancellationToken ct = default)
    {
        // HH:mm format allows safe string comparison (lexicographic == chronological)
        return await db.Bookings
            .Include(b => b.Class)
            .Where(b =>
                b.StudentId == studentId &&
                b.Status != BookingStatus.cancelled &&
                b.Class!.Date == date &&
                b.ClassId != excludeClassId &&
                string.Compare(b.Class.StartTime, endTime) < 0 &&
                string.Compare(b.Class.EndTime, startTime) > 0)
            .AnyAsync(ct);
    }

    public async Task<bool> IsBikeOccupiedAsync(Guid classId, int bikeNumber, CancellationToken ct = default)
        => await db.Bookings.AnyAsync(b =>
            b.ClassId == classId &&
            b.BikeNumber == bikeNumber &&
            b.Status != BookingStatus.cancelled, ct);

    public async Task AddAsync(Booking booking, CancellationToken ct = default)
        => await db.Bookings.AddAsync(booking, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}