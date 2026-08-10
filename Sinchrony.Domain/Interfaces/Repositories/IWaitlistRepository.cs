using Sinchrony.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Domain.Interfaces.Repositories
{
    public interface IWaitlistRepository
    {
        Task<WaitlistEntry?> GetByClassAndStudentAsync(Guid classId, Guid studentId, CancellationToken ct = default);
        Task<IEnumerable<WaitlistEntry>> ListByClassAsync(Guid classId, CancellationToken ct = default);
        Task<WaitlistEntry?> GetNextWaitingAsync(Guid classId, CancellationToken ct = default);
        Task<int> CountByClassAsync(Guid classId, CancellationToken ct = default);
        Task AddAsync(WaitlistEntry entry, CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
    }
}
