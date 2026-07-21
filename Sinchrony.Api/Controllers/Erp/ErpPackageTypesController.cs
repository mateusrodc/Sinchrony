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
        pt.Update(req.name, req.active ?? pt.Active, req.isFamily ?? pt.IsFamily, req.rank,
            req.defaultMaxFutureBookings, req.defaultMaxBookingsPerDay,
            req.defaultMaxBookingsPerWeek, req.defaultMaxBookingsPerMonth,
            req.defaultCancellationDeadlineHours, req.defaultBookingWindowDays,
            req.defaultEarlyAccessHours, req.defaultAllowWaitlist,
            req.defaultReschedulingAllowed, req.defaultReschedulingDeadlineHours,
            req.defaultNoShowCreditPenalty, req.defaultMaxNoShowsBeforeBlock);
        await packageTypeRepository.SaveAsync(ct);
        return Ok(MapPackageType(pt));
    }
}

public record PackageTypeRequest(
    string name, bool? active, bool? isFamily, int? rank,
    int? defaultMaxFutureBookings, int? defaultMaxBookingsPerDay,
    int? defaultMaxBookingsPerWeek, int? defaultMaxBookingsPerMonth,
    int? defaultCancellationDeadlineHours, int? defaultBookingWindowDays,
    int? defaultEarlyAccessHours, bool? defaultAllowWaitlist,
    bool? defaultReschedulingAllowed, int? defaultReschedulingDeadlineHours,
    bool? defaultNoShowCreditPenalty, int? defaultMaxNoShowsBeforeBlock);