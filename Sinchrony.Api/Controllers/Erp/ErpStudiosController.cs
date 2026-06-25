using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/studios")]
[Produces("application/json")]
public class ErpStudiosController(IStudioRepository studioRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(StudioListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var studios = await studioRepository.ListAsync(ct);
        return Ok(new { data = studios.Select(MapStudio) });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(StudioListResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var studio = await studioRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Studio not found.");
        return Ok(MapStudio(studio));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StudioRequest req, CancellationToken ct)
    {
        var studio = Studio.Create(req.name, req.address, req.capacity,
            req.phone, req.email, req.openingTime ?? "06:00", req.closingTime ?? "22:00");
        await studioRepository.AddAsync(studio, ct);
        await studioRepository.SaveAsync(ct);
        return StatusCode(201, MapStudio(studio));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] StudioRequest req, CancellationToken ct)
    {
        var studio = await studioRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Studio not found.");

        studio.Update(req.name, req.address, req.capacity,
            req.phone, req.email,
            req.openingTime ?? "06:00", req.closingTime ?? "22:00", req.active ?? true);
        await studioRepository.SaveAsync(ct);
        return Ok(MapStudio(studio));
    }

    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var studio = await studioRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Studio not found.");

        studio.Update(studio.Name, studio.Address, studio.Capacity,
            studio.Phone, studio.Email,
            studio.OpeningTime, studio.ClosingTime, active: false);

        await studioRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var studio = await studioRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Studio not found.");

        studio.Update(studio.Name, studio.Address, studio.Capacity,
            studio.Phone, studio.Email,
            studio.OpeningTime, studio.ClosingTime, active: true);

        await studioRepository.SaveAsync(ct);
        return Ok(new { success = true });
    }

    private static object MapStudio(Studio s) => new
    {
        id = s.Id,
        name = s.Name,
        address = s.Address,
        phone = s.Phone,
        email = s.Email,
        active = s.Active,
        capacity = s.Capacity,
        openingTime = s.OpeningTime,
        closingTime = s.ClosingTime
    };
}

public record StudioRequest(string name, string address, int capacity,
    string? phone, string? email, string? openingTime, string? closingTime, bool? active);