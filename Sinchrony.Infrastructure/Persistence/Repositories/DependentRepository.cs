using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class DependentRepository(ApplicationDbContext db) : IDependentRepository
{
    public async Task<IEnumerable<Dependent>> ListByStudentAsync(Guid studentId, CancellationToken ct = default)
    => await db.Dependents
        .Include(d => d.User)  // <-- inclui User
        .Where(d => d.ResponsibleStudentId == studentId && d.Active)
        .OrderBy(d => d.Name)
        .ToListAsync(ct);

    public async Task<Dependent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Dependents
            .Include(d => d.User)  // <-- inclui User
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddAsync(Dependent dependent, CancellationToken ct = default)
        => await db.Dependents.AddAsync(dependent, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}