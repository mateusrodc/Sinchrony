using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/units")]
[Produces("application/json")]
public class ErpUnitsController(
    IUnitRepository unitRepository,
    IUnitContext unitContext,
    IUserRepository userRepository,
    IClassRepository classRepository,
    IBookingRepository bookingRepository,
    IPurchaseRepository purchaseRepository) : ControllerBase
{
    private static object MapUnit(Unit u) => new
    {
        id = u.Id,
        name = u.Name,
        address = u.Address,
        phone = u.Phone,
        email = u.Email,
        active = u.Active,
        studiosCount = u.Studios.Count,
        createdAt = u.CreatedAt
    };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        // Só admin global vê todas as unidades
        if (!unitContext.IsGlobalAdmin)
            return Forbid();

        var units = await unitRepository.ListAsync(ct);
        return Ok(new { data = units.Select(MapUnit) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        // Admin de unidade só vê a própria unidade
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId != id)
            return Forbid();

        var unit = await unitRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Unit not found.");

        return Ok(MapUnit(unit));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnitRequest req, CancellationToken ct)
    {
        if (!unitContext.IsGlobalAdmin)
            return Forbid();

        var unit = Unit.Create(req.name, req.address, req.phone, req.email);
        await unitRepository.AddAsync(unit, ct);
        await unitRepository.SaveAsync(ct);
        return StatusCode(201, MapUnit(unit));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UnitRequest req, CancellationToken ct)
    {
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId != id)
            return Forbid();

        var unit = await unitRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Unit not found.");

        unit.Update(req.name, req.address, req.phone, req.email, req.active ?? true);
        await unitRepository.SaveAsync(ct);
        return Ok(MapUnit(unit));
    }

    [HttpGet("{id}/dashboard")]
    public async Task<IActionResult> Dashboard(Guid id, CancellationToken ct)
    {
        if (!unitContext.IsGlobalAdmin && unitContext.UnitId != id)
            return Forbid();

        var unit = await unitRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Unit not found.");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var students = await userRepository.ListStudentsByUnitAsync(id, ct);
        var classes = await classRepository.ListAsync(null, null, null, ct);
        var unitStudioIds = unit.Studios.Select(s => s.Id).ToHashSet();
        var unitClasses = classes.Where(c => unitStudioIds.Contains(c.StudioId)).ToList();
        var monthClasses = unitClasses.Where(c =>
            c.Date.Month == now.Month && c.Date.Year == now.Year).ToList();

        var allPurchases = await purchaseRepository.ListAllAsync(ct);
        var revenue = allPurchases
            .Where(p => p.Status == "confirmed" &&
                p.CreatedAt.Month == now.Month && p.CreatedAt.Year == now.Year &&
                students.Any(s => s.Id == p.UserId))
            .Sum(p => p.Amount);

        var totalSpots = monthClasses.Sum(c => c.TotalSpots);
        var allBookings = await bookingRepository.ListErpAsync(null, null, null, ct);
        var unitBookings = allBookings.Where(b =>
            b.Class != null && unitStudioIds.Contains(b.Class.StudioId)).ToList();
        var monthBookings = unitBookings.Where(b =>
            b.Class != null &&
            b.Class.Date.Month == now.Month && b.Class.Date.Year == now.Year &&
            b.Status == Domain.Enums.BookingStatus.confirmed).ToList();

        var occupancyRate = totalSpots > 0
            ? Math.Round((double)monthBookings.Count * 100 / totalSpots, 1)
            : 0;

        return Ok(new
        {
            unitId = unit.Id,
            unitName = unit.Name,
            totalStudents = students.Count(),
            totalClassesThisMonth = monthClasses.Count,
            revenueThisMonth = revenue,
            occupancyRate,
            activeStudents = students.Count(s => s.Status == Domain.Enums.StudentStatus.active)
        });
    }
}

public record UnitRequest(string name, string? address, string? phone, string? email, bool? active);