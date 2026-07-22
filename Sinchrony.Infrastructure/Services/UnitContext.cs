using Microsoft.AspNetCore.Http;
using Sinchrony.Domain.Interfaces.Services;
using System.Security.Claims;

namespace Sinchrony.Infrastructure.Services;

public class UnitContext(IHttpContextAccessor httpContextAccessor) : IUnitContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

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
}