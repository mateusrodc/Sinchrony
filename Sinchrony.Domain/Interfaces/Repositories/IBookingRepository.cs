using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Booking>> ListByStudentAsync(Guid studentId, string? status, bool history, CancellationToken ct = default);
    Task<IEnumerable<Booking>> ListErpAsync(Guid? classId, Guid? studentId, string? status, CancellationToken ct = default);
    Task<bool> HasActiveBookingAsync(Guid studentId, Guid classId, CancellationToken ct = default);
    Task<bool> HasTimeConflictAsync(Guid studentId, DateOnly date, string startTime, string endTime, Guid? excludeClassId, CancellationToken ct = default);
    Task<bool> IsBikeOccupiedAsync(Guid classId, int bikeNumber, CancellationToken ct = default);
    Task AddAsync(Booking booking, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}