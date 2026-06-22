using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Packages;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("packages")]
[Produces("application/json")]
public class PackagesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(PackageListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await mediator.Send(new ListPackagesQuery(ActiveOnly: true), ct);
        return Ok(new { data = result });
    }
}