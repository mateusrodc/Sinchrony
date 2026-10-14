using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Interfaces.Repositories;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

// Permissões por módulo de quem está logado, para o ERP e o App esconderem os botões que a pessoa
// não pode usar. Só admin tem permissão por módulo; professor/aluno recebe lista vazia (o papel
// continua sendo o que vale para eles). Não confundir com /auth/me/permissions, que é a lista
// fixa por papel usada pelo App.
[Authorize]
[ApiController]
[Route("api/me")]
[Produces("application/json")]
public class ErpMeController(IPermissionRepository permissionRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet("permissions")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> Permissions(CancellationToken ct)
    {
        if (!User.IsInRole("admin"))
            return Ok(new { data = Array.Empty<object>(), fullAccess = false });

        var catalogSize = (await permissionRepository.ListCatalogAsync(ct)).Count();
        var granted = (await permissionRepository.ListByUserAsync(UserId, ct)).ToList();
        return Ok(new
        {
            data = granted.Select(p => new { resource = p.Resource, action = p.Action }),
            fullAccess = granted.Count == catalogSize
        });
    }
}
