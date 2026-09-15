using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PermissionRepository(ApplicationDbContext db) : IPermissionRepository
{
    public async Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default)
        => await db.UserPermissions.AnyAsync(up =>
            up.UserId == userId
            && up.Permission!.Resource == resource
            && up.Permission!.Action == action, ct);
}
