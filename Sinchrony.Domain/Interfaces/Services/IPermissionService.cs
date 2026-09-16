namespace Sinchrony.Domain.Interfaces.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default);

    // Limpa o cache de HasPermissionAsync do usuário — chamado depois de ReplaceForUserAsync
    // pra evitar até 5 min de permissão desatualizada (cache antigo continuaria respondendo com
    // o conjunto anterior).
    Task InvalidateUserCacheAsync(Guid userId, CancellationToken ct = default);
}
