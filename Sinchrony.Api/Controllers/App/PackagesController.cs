using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Packages.Queries.ListPackages;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("packages")]
public class PackagesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await mediator.Send(new ListPackagesQuery(ActiveOnly: true), ct);
        return Ok(new { data = result });
    }
}