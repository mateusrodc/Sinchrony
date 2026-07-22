using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Packages;
using Sinchrony.Application.Packages.Commands.PurchasePackage;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("packages")]
[Produces("application/json")]
public class PackagesController(IMediator mediator, IPackageRepository packageRepository) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

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
    [HttpPost("{packageId}/purchase")]
    public async Task<IActionResult> Purchase(
    Guid packageId,
    [FromBody] PurchasePackageRequest req,
    CancellationToken ct)
    {
        var result = await mediator.Send(
            new PurchasePackageCommand(UserId, packageId, req.paymentMethod,
                req.amount, req.cardToken, req.cpf, req.couponCode), ct);
        return StatusCode(201, result);
    }

    public record PurchasePackageRequest(
        string paymentMethod,
        decimal amount,
        string? cardToken,
        string? cpf,
        string? couponCode);
}