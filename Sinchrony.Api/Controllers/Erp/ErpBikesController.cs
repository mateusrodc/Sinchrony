using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
public class ErpBikesController(IBikeRepository bikeRepository) : ControllerBase
{
    [HttpGet("api/studios/{studioId}/bikes")]
    public async Task<IActionResult> List(Guid studioId, CancellationToken ct)
    {
        var bikes = await bikeRepository.ListByStudioAsync(studioId, ct);
        return Ok(new { data = bikes.Select(MapBike) });
    }

    [HttpPost("api/studios/{studioId}/bikes")]
    public async Task<IActionResult> Create(Guid studioId, [FromBody] CreateBikeRequest req, CancellationToken ct)
    {
        var bike = Bike.Create(studioId, req.number);
        await bikeRepository.AddAsync(bike, ct);
        await bikeRepository.SaveAsync(ct);
        return StatusCode(201, MapBike(bike));
    }

    [HttpPut("api/bikes/{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBikeRequest req, CancellationToken ct)
    {
        var bike = await bikeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Bike not found.");

        var status = Enum.Parse<BikeStatus>(req.status, ignoreCase: true);
        DateOnly? maintenance = req.lastMaintenance is not null
            ? DateOnly.Parse(req.lastMaintenance)
            : null;

        bike.Update(status, req.notes, maintenance);
        await bikeRepository.SaveAsync(ct);
        return Ok(MapBike(bike));
    }

    [HttpDelete("api/bikes/{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var bike = await bikeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Bike not found.");
        await bikeRepository.RemoveAsync(bike, ct);
        await bikeRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }

    private static object MapBike(Bike b) => new
    {
        id = b.Id,
        studioId = b.StudioId,
        number = b.Number,
        status = b.Status.ToString(),
        lastMaintenance = b.LastMaintenance?.ToString("yyyy-MM-dd"),
        notes = b.Notes
    };
}

public record CreateBikeRequest(int number, string status = "available");
public record UpdateBikeRequest(string status, string? notes, string? lastMaintenance);