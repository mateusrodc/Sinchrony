using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Domain.Services;

// ÚNICO ponto de decisão sobre "quais unidades este usuário enxerga". Toda rota do ERP que
// restringe por unidade passa por aqui (a regra vale com User.UnitId OU com as unidades marcadas
// em "Unidades (filiais)", que é o cadastro normal de quem é criado pelo ERP). Comparar
// `unitContext.UnitId` direto, ou responder `Forbid()`, reabre a falha em que a secretária sem
// User.UnitId voltava para o login — há um teste que impede isso (UnitScopeGuardTests).
public static class UnitScope
{
    // null = sem restrição (admin global). Senão, as unidades que o usuário gerencia; sem nenhuma,
    // falha fechado com 403 NO_UNIT_ASSIGNED.
    public static async Task<IReadOnlyList<Guid>?> GetScopeAsync(
        this IUnitContext unitContext, CancellationToken ct = default)
    {
        if (unitContext.IsGlobalAdmin) return null;

        var managed = await unitContext.GetManagedUnitIdsAsync(ct);
        if (managed.Count == 0)
            throw DomainException.NoUnitAssigned();
        return managed;
    }

    // Registro de uma unidade (unitId). Registro sem unidade (ex.: aluno antigo) é visível a
    // qualquer admin enquanto houver uma unidade só. Fora do escopo: 403 UNIT_FORBIDDEN.
    public static async Task EnsureCanAccessUnitAsync(
        this IUnitContext unitContext, Guid? unitId, CancellationToken ct = default)
    {
        var scope = await unitContext.GetScopeAsync(ct);
        if (scope is null || unitId is null) return;
        if (!scope.Contains(unitId.Value))
            throw DomainException.UnitForbidden();
    }

    // Unidade para gravar em registros novos quando o pedido não informa uma: a do cadastro, ou a
    // única que o usuário gerencia. Várias unidades e nenhuma escolhida: sem unidade.
    public static async Task<Guid?> ResolveDefaultUnitIdAsync(
        this IUnitContext unitContext, CancellationToken ct = default)
    {
        if (unitContext.UnitId.HasValue) return unitContext.UnitId;
        var managed = await unitContext.GetManagedUnitIdsAsync(ct);
        return managed.Count == 1 ? managed[0] : null;
    }
}
