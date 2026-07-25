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
[Route("api/class-types")]
[Produces("application/json")]
public class ErpClassTypesController(IClassTypeRepository classTypeRepository) : ControllerBase
{
    private static object MapClassType(ClassType ct) => new
    {
        id = ct.Id,
        name = ct.Name,
        active = ct.Active,
        usesBikes = ct.UsesBikes
    };

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassTypeListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await classTypeRepository.ListAsync(ct);
        return Ok(new { data = items.Select(MapClassType) });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassTypeListResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var classType = await classTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("ClassType not found.");
        return Ok(MapClassType(classType));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClassTypeRequest req, CancellationToken ct)
    {
        var classType = ClassType.Create(req.name);
        classType.Update(req.name, req.active ?? true, req.usesBikes ?? false);
        await classTypeRepository.AddAsync(classType, ct);
        await classTypeRepository.SaveAsync(ct);
        return StatusCode(201, MapClassType(classType));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ClassTypeRequest req, CancellationToken ct)
    {
        var classType = await classTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("ClassType not found.");
        classType.Update(req.name ?? classType.Name, req.active ?? classType.Active, req.usesBikes ?? classType.UsesBikes);
        await classTypeRepository.SaveAsync(ct);
        return Ok(MapClassType(classType));
    }
}

public record ClassTypeRequest(string? name, bool? active, bool? usesBikes);