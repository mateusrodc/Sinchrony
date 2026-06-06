using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class ClassTypeRepository(ApplicationDbContext db) : IClassTypeRepository
{
    public async Task<ClassType?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ClassTypes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IEnumerable<ClassType>> ListAsync(CancellationToken ct = default)
        => await db.ClassTypes.OrderBy(c => c.Name).ToListAsync(ct);

    public async Task AddAsync(ClassType classType, CancellationToken ct = default)
        => await db.ClassTypes.AddAsync(classType, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}