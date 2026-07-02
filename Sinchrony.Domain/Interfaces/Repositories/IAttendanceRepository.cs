using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IAttendanceRepository
{
    Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AttendanceRecord?> GetByClassAndStudentAsync(Guid classId, Guid studentId, CancellationToken ct = default);
    Task<IEnumerable<AttendanceRecord>> ListByClassAsync(Guid classId, CancellationToken ct = default);
    Task AddAsync(AttendanceRecord record, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<IEnumerable<AttendanceRecord>> ListAllAsync(CancellationToken ct = default);
    Task<AttendanceRecord?> GetByBookingAsync(
    Guid bookingId, CancellationToken ct = default);
}