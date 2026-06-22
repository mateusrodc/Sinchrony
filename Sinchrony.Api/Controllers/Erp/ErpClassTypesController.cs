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
    [HttpGet]
    [SwaggerResponseExample(200, typeof(ClassTypeListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var types = await classTypeRepository.ListAsync(ct);
        return Ok(new { data = types.Select(t => new { id = t.Id, name = t.Name, active = t.Active }) });
    }

    [HttpGet("{id}")]
    [SwaggerResponseExample(200, typeof(ClassTypeListResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var type = await classTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class type not found.");
        return Ok(new { id = type.Id, name = type.Name, active = type.Active });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClassTypeRequest req, CancellationToken ct)
    {
        var type = ClassType.Create(req.name);
        await classTypeRepository.AddAsync(type, ct);
        await classTypeRepository.SaveAsync(ct);
        return StatusCode(201, new { id = type.Id, name = type.Name, active = type.Active });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ClassTypeRequest req, CancellationToken ct)
    {
        var type = await classTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class type not found.");
        type.Update(req.name, req.active ?? type.Active);
        await classTypeRepository.SaveAsync(ct);
        return Ok(new { id = type.Id, name = type.Name, active = type.Active });
    }
}

public record ClassTypeRequest(string name, bool? active);