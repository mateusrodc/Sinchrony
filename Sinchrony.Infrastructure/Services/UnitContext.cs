using Microsoft.AspNetCore.Http;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Infrastructure.Services;

public class UnitContext(IHttpContextAccessor httpContextAccessor, ITeacherUnitRepository teacherUnitRepository) : IUnitContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    // Scoped: as unidades do cadastro são lidas uma vez por requisição.
    private IReadOnlyList<Guid>? _managedUnitIds;

    public Guid? UnitId
    {
        get
        {
            var claim = User?.FindFirstValue("unitId");
            return string.IsNullOrEmpty(claim) ? null : Guid.Parse(claim);
        }
    }

    public bool IsGlobalAdmin
    {
        get
        {
            var claim = User?.FindFirstValue("isGlobalAdmin");
            return claim == "true";
        }
    }

    public Guid? UserId
    {
        get
        {
            var claim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");
            return string.IsNullOrEmpty(claim) ? null : Guid.Parse(claim);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetManagedUnitIdsAsync(CancellationToken ct = default)
    {
        if (_managedUnitIds is not null) return _managedUnitIds;

        var ids = new List<Guid>();
        if (UnitId.HasValue) ids.Add(UnitId.Value);
        if (UserId.HasValue)
            ids.AddRange((await teacherUnitRepository.ListByTeacherAsync(UserId.Value, ct)).Select(tu => tu.UnitId));

        return _managedUnitIds = ids.Distinct().ToList();
    }
}
