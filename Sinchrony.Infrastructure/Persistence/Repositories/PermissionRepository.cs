using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class PermissionRepository(ApplicationDbContext db) : IPermissionRepository
{
    public async Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default)
        => await db.UserPermissions.AnyAsync(up =>
            up.UserId == userId
            && up.Permission!.Resource == resource
            && up.Permission!.Action == action, ct);

    public async Task<IEnumerable<Permission>> ListCatalogAsync(CancellationToken ct = default)
        => await db.Permissions
            .OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .ToListAsync(ct);

    public async Task<IEnumerable<Permission>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission!)
            .OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .ToListAsync(ct);

    public async Task ReplaceForUserAsync(
        Guid userId, IEnumerable<Guid> permissionIds, Guid? grantedByUserId, CancellationToken ct = default)
    {
        var existing = await db.UserPermissions.Where(up => up.UserId == userId).ToListAsync(ct);
        db.UserPermissions.RemoveRange(existing);

        foreach (var permissionId in permissionIds.Distinct())
            db.UserPermissions.Add(UserPermission.Grant(userId, permissionId, grantedByUserId));

        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<Guid>> ListUserIdsWithFullAccessAsync(int catalogSize, CancellationToken ct = default)
        => await db.UserPermissions
            .GroupBy(up => up.UserId)
            .Where(g => g.Count() == catalogSize)
            .Select(g => g.Key)
            .ToListAsync(ct);
}
