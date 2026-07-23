using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Application.Packages.Commands.CreatePackage;
using Sinchrony.Application.Packages.Commands.TogglePackage;
using Sinchrony.Application.Packages.Commands.UpdatePackage;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/packages")]
[Produces("application/json")]
public class ErpPackagesController(IMediator mediator, IPackageRepository packageRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpPackageListResponseExample))]
    public async Task<IActionResult> List([FromQuery] bool? activeOnly, CancellationToken ct)
    {
        var result = await mediator.Send(new ListPackagesQuery(activeOnly), ct);
        return Ok(new { data = result });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ErpPackageListResponseExample))]
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
            req.validityDays, req.popular, req.active, req.displayOrder,
            req.packageTypeId, req.purchaseStrategy ?? "block",
            req.maxDependents ?? 0, req.creditsPerMember,
            req.maxFutureBookings, req.maxBookingsPerDay,
            req.maxBookingsPerWeek, req.maxBookingsPerMonth,
            req.cancellationDeadlineHours, req.bookingWindowDays,
            req.earlyAccessHours, req.allowWaitlist, req.waitlistPriority,
            req.reschedulingAllowed, req.reschedulingDeadlineHours,
            req.noShowCreditPenalty ?? true, req.maxNoShowsBeforeBlock,
            req.noShowBlockWindowDays ?? 30,
            req.benefitIds), ct);
        return StatusCode(201, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePackageRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdatePackageCommand(
            id, req.name, req.description, req.credits, req.price,
            req.validityDays, req.popular, req.active, req.displayOrder,
            req.packageTypeId, req.purchaseStrategy ?? "block",
            req.maxDependents ?? 0, req.creditsPerMember,
            req.maxFutureBookings, req.maxBookingsPerDay,
            req.maxBookingsPerWeek, req.maxBookingsPerMonth,
            req.cancellationDeadlineHours, req.bookingWindowDays,
            req.earlyAccessHours, req.allowWaitlist, req.waitlistPriority,
            req.reschedulingAllowed, req.reschedulingDeadlineHours,
            req.noShowCreditPenalty ?? true, req.maxNoShowsBeforeBlock,
            req.noShowBlockWindowDays ?? 30,
            req.benefitIds ?? []), ct);
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
    int validityDays, bool popular, bool active, int displayOrder,
    Guid? packageTypeId = null,
    string? purchaseStrategy = null,
    int? maxDependents = null,
    int? creditsPerMember = null,
    int? maxFutureBookings = null,
    int? maxBookingsPerDay = null,
    int? maxBookingsPerWeek = null,
    int? maxBookingsPerMonth = null,
    int? cancellationDeadlineHours = null,
    int? bookingWindowDays = null,
    int? earlyAccessHours = null,
    bool? allowWaitlist = null,
    int? waitlistPriority = null,
    bool? reschedulingAllowed = null,
    int? reschedulingDeadlineHours = null,
    bool? noShowCreditPenalty = null,
    int? maxNoShowsBeforeBlock = null,
    int? noShowBlockWindowDays = null,
    List<Guid>? benefitIds = null);

public record UpdatePackageRequest(
    string name, string? description, int credits, decimal price,
    int validityDays, bool popular, bool active, int displayOrder,
    Guid packageTypeId,
    string? purchaseStrategy,
    int? maxDependents,
    int? creditsPerMember,
    int? maxFutureBookings,
    int? maxBookingsPerDay,
    int? maxBookingsPerWeek,
    int? maxBookingsPerMonth,
    int? cancellationDeadlineHours,
    int? bookingWindowDays,
    int? earlyAccessHours,
    bool? allowWaitlist,
    int? waitlistPriority,
    bool? reschedulingAllowed,
    int? reschedulingDeadlineHours,
    bool? noShowCreditPenalty,
    int? maxNoShowsBeforeBlock,
    int? noShowBlockWindowDays,
    List<Guid>? benefitIds);