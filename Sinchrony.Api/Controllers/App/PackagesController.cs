using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Packages;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("packages")]
[Produces("application/json")]
public class PackagesController(IMediator mediator, IPackageRepository packageRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(PackageListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await mediator.Send(new ListPackagesQuery(ActiveOnly: true), ct);
        return Ok(new { data = result });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(PackageListResponseExample))]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var package = await packageRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Package not found.");

        return Ok(new { data = ListPackagesQueryHandler.MapToDto(package) });
    }
}