namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IPermissionRepository
{
    Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default);
}
