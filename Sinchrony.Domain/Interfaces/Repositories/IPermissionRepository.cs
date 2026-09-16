using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IPermissionRepository
{
    Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default);

    // Fase 3 — Gestão de Permissões por Usuário (DEMANDA_CONTROLE_ADMIN_PERMISSOES_FASE3_BACKEND.md)
    Task<IEnumerable<Permission>> ListCatalogAsync(CancellationToken ct = default);
    Task<IEnumerable<Permission>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task ReplaceForUserAsync(Guid userId, IEnumerable<Guid> permissionIds, Guid? grantedByUserId, CancellationToken ct = default);

    // Usuários cujo total de permissões concedidas bate com o tamanho do catálogo — proxy pra
    // "tem acesso total" sem precisar comparar conjunto item a item (seguro porque
    // UserPermission tem índice único em (UserId, PermissionId), não há duplicata).
    Task<IEnumerable<Guid>> ListUserIdsWithFullAccessAsync(int catalogSize, CancellationToken ct = default);
}
