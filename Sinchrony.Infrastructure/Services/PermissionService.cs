using Microsoft.Extensions.Caching.Memory;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

// Cache curto em memória (5 min) — permissão muda raramente, não vale bater no banco a cada
// request. Cacheia por (userId, resource, action), não a lista inteira do usuário: mais
// simples e o volume de combinações checadas por usuário numa sessão é baixo.
public class PermissionService(IPermissionRepository repository, IMemoryCache cache) : IPermissionService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default)
    {
        var cacheKey = $"permission:{userId}:{resource}:{action}";
        return cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await repository.HasPermissionAsync(userId, resource, action, ct);
        })!;
    }

    public async Task InvalidateUserCacheAsync(Guid userId, CancellationToken ct = default)
    {
        var catalog = await repository.ListCatalogAsync(ct);
        foreach (var permission in catalog)
            cache.Remove($"permission:{userId}:{permission.Resource}:{permission.Action}");
    }
}
