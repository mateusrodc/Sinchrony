using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/package-types")]
[Produces("application/json")]
public class ErpPackageTypesController(IPackageTypeRepository packageTypeRepository) : ControllerBase
{
    private static object MapPackageType(PackageType pt) => new
    {
        id = pt.Id,
        name = pt.Name,
        active = pt.Active,
        isFamily = pt.IsFamily,
        rank = pt.Rank,
        defaultMaxFutureBookings = pt.DefaultMaxFutureBookings,
        defaultMaxBookingsPerDay = pt.DefaultMaxBookingsPerDay,
        defaultMaxBookingsPerWeek = pt.DefaultMaxBookingsPerWeek,
        defaultMaxBookingsPerMonth = pt.DefaultMaxBookingsPerMonth,
        defaultCancellationDeadlineHours = pt.DefaultCancellationDeadlineHours,
        defaultBookingWindowDays = pt.DefaultBookingWindowDays,
        defaultEarlyAccessHours = pt.DefaultEarlyAccessHours,
        defaultAllowWaitlist = pt.DefaultAllowWaitlist,
        defaultReschedulingAllowed = pt.DefaultReschedulingAllowed,
        defaultReschedulingDeadlineHours = pt.DefaultReschedulingDeadlineHours,
        defaultNoShowCreditPenalty = pt.DefaultNoShowCreditPenalty,
        defaultMaxNoShowsBeforeBlock = pt.DefaultMaxNoShowsBeforeBlock
    };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await packageTypeRepository.ListAsync(ct);
        return Ok(new { data = items.Select(MapPackageType) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var pt = await packageTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("PackageType not found.");
        return Ok(MapPackageType(pt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PackageTypeRequest req, CancellationToken ct)
    {
        var pt = PackageType.Create(req.name, req.isFamily ?? false, req.rank);
        pt.Update(req.name, true, req.isFamily ?? false, req.rank,
            req.defaultMaxFutureBookings, req.defaultMaxBookingsPerDay,
            req.defaultMaxBookingsPerWeek, req.defaultMaxBookingsPerMonth,
            req.defaultCancellationDeadlineHours, req.defaultBookingWindowDays,
            req.defaultEarlyAccessHours, req.defaultAllowWaitlist,
            req.defaultReschedulingAllowed, req.defaultReschedulingDeadlineHours,
            req.defaultNoShowCreditPenalty, req.defaultMaxNoShowsBeforeBlock);
        await packageTypeRepository.AddAsync(pt, ct);
        await packageTypeRepository.SaveAsync(ct);
        return StatusCode(201, MapPackageType(pt));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PackageTypeRequest req, CancellationToken ct)
    {
        var pt = await packageTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("PackageType not found.");

        pt.Update(
            req.name ?? pt.Name,
            req.active ?? pt.Active,
            req.isFamily ?? pt.IsFamily,
            req.rank ?? pt.Rank,
            req.defaultMaxFutureBookings ?? pt.DefaultMaxFutureBookings,
            req.defaultMaxBookingsPerDay ?? pt.DefaultMaxBookingsPerDay,
            req.defaultMaxBookingsPerWeek ?? pt.DefaultMaxBookingsPerWeek,
            req.defaultMaxBookingsPerMonth ?? pt.DefaultMaxBookingsPerMonth,
            req.defaultCancellationDeadlineHours ?? pt.DefaultCancellationDeadlineHours,
            req.defaultBookingWindowDays ?? pt.DefaultBookingWindowDays,
            req.defaultEarlyAccessHours ?? pt.DefaultEarlyAccessHours,
            req.defaultAllowWaitlist ?? pt.DefaultAllowWaitlist,
            req.defaultReschedulingAllowed ?? pt.DefaultReschedulingAllowed,
            req.defaultReschedulingDeadlineHours ?? pt.DefaultReschedulingDeadlineHours,
            req.defaultNoShowCreditPenalty ?? pt.DefaultNoShowCreditPenalty,
            req.defaultMaxNoShowsBeforeBlock ?? pt.DefaultMaxNoShowsBeforeBlock);

        await packageTypeRepository.SaveAsync(ct);
        return Ok(MapPackageType(pt));
    }
}

public record PackageTypeRequest(
    string? name = null,
    bool? active = null,
    bool? isFamily = null,
    int? rank = null,
    int? defaultMaxFutureBookings = null,
    int? defaultMaxBookingsPerDay = null,
    int? defaultMaxBookingsPerWeek = null,
    int? defaultMaxBookingsPerMonth = null,
    int? defaultCancellationDeadlineHours = null,
    int? defaultBookingWindowDays = null,
    int? defaultEarlyAccessHours = null,
    bool? defaultAllowWaitlist = null,
    bool? defaultReschedulingAllowed = null,
    int? defaultReschedulingDeadlineHours = null,
    bool? defaultNoShowCreditPenalty = null,
    int? defaultMaxNoShowsBeforeBlock = null);