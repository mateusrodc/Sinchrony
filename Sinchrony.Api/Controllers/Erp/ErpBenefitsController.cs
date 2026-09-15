using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Erp;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/benefits")]
[Produces("application/json")]
public class ErpBenefitsController(
    IBenefitRepository benefitRepository,
    IAuditService auditService) : ControllerBase
{
    private Guid AdminId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    private static object MapBenefit(Benefit b) => new
    {
        id = b.Id,
        name = b.Name,
        description = b.Description,
        icon = b.Icon,
        active = b.Active
    };

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(BenefitListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await benefitRepository.ListAsync(ct);
        return Ok(new { data = items.Select(MapBenefit) });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(BenefitListResponseExample))]
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

        await auditService.LogAsync("benefit.created", "Benefit", benefit.Id, AdminId, $"Name: {benefit.Name}", ct: ct);

        return StatusCode(201, MapBenefit(benefit));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BenefitRequest req, CancellationToken ct)
    {
        var benefit = await benefitRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Benefit not found.");
        benefit.Update(req.name, req.description, req.icon, req.active ?? true);
        await benefitRepository.SaveAsync(ct);

        await auditService.LogAsync("benefit.updated", "Benefit", benefit.Id, AdminId, $"Name: {benefit.Name}", ct: ct);

        return Ok(MapBenefit(benefit));
    }
}

public record BenefitRequest(string name, string? description, string? icon, bool? active);