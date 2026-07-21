using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IStudentPackageRepository
{
    Task<StudentPackage?> GetActiveByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<StudentPackage?> GetQueuedByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<IEnumerable<StudentPackage>> ListByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task AddAsync(StudentPackage studentPackage, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}