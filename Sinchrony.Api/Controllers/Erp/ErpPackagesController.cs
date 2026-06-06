using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Packages.Commands.CreatePackage;
using Sinchrony.Application.Packages.Commands.TogglePackage;
using Sinchrony.Application.Packages.Commands.UpdatePackage;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/packages")]
public class ErpPackagesController(IMediator mediator, IPackageRepository packageRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activeOnly, CancellationToken ct)
    {
        var result = await mediator.Send(new ListPackagesQuery(activeOnly), ct);
        return Ok(new { data = result });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var pkg = await packageRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Package not found.");
        return Ok(ListPackagesQueryHandler.MapToDto(pkg));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePackageRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreatePackageCommand(
            req.name, req.description, req.credits, req.price,
            req.validityDays, req.popular, req.active, req.displayOrder), ct);
        return StatusCode(201, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePackageRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdatePackageCommand(
            id, req.name, req.description, req.credits, req.price,
            req.validityDays, req.popular, req.active, req.displayOrder), ct);
        return Ok(result);
    }

    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new TogglePackageCommand(id), ct);
        return Ok(result);
    }
}

public record CreatePackageRequest(
    string name, string? description, int credits, decimal price,
    int validityDays, bool popular, bool active, int displayOrder);

public record UpdatePackageRequest(
    string name, string? description, int credits, decimal price,
    int validityDays, bool popular, bool active, int displayOrder);