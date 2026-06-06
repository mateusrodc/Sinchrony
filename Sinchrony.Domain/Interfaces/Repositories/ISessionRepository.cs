using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface ISessionRepository
{
    Task<ClassSession?> GetByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task AddAsync(ClassSession session, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}