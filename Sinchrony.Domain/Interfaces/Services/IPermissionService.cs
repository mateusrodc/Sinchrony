namespace Sinchrony.Domain.Interfaces.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default);
}
