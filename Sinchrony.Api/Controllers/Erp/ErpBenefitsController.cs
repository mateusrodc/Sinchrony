using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/benefits")]
[Produces("application/json")]
public class ErpBenefitsController(IBenefitRepository benefitRepository) : ControllerBase
{
    private static object MapBenefit(Benefit b) => new
    {
        id = b.Id,
        name = b.Name,
        description = b.Description,
        icon = b.Icon,
        active = b.Active
    };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await benefitRepository.ListAsync(ct);
        return Ok(new { data = items.Select(MapBenefit) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var benefit = await benefitRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Benefit not found.");
        return Ok(MapBenefit(benefit));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BenefitRequest req, CancellationToken ct)
    {
        var benefit = Benefit.Create(req.name, req.description, req.icon);
        await benefitRepository.AddAsync(benefit, ct);
        await benefitRepository.SaveAsync(ct);
        return StatusCode(201, MapBenefit(benefit));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BenefitRequest req, CancellationToken ct)
    {
        var benefit = await benefitRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Benefit not found.");
        benefit.Update(req.name, req.description, req.icon, req.active ?? true);
        await benefitRepository.SaveAsync(ct);
        return Ok(MapBenefit(benefit));
    }
}

public record BenefitRequest(string name, string? description, string? icon, bool? active);