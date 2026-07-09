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
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassTypeListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var types = await classTypeRepository.ListAsync(ct);
        return Ok(new { data = types.Select(t => new { id = t.Id, name = t.Name, active = t.Active }) });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(ClassTypeListResponseExample))]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var type = await classTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class type not found.");
        return Ok(new { id = type.Id, name = type.Name, active = type.Active });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassTypeRequest req, CancellationToken ct)
    {
        var classType = ClassType.Create(req.name);
        classType.Update(req.name, req.active, req.usesBikes);
        await classTypeRepository.AddAsync(classType, ct);
        await classTypeRepository.SaveAsync(ct);
        return StatusCode(201, MapClassType(classType));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassTypeRequest req, CancellationToken ct)
    {
        var classType = await classTypeRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("ClassType not found.");

        classType.Update(req.name, req.active, req.usesBikes);
        await classTypeRepository.SaveAsync(ct);
        return Ok(MapClassType(classType));
    }
    private static object MapClassType(ClassType ct) => new
    {
        id = ct.Id,
        name = ct.Name,
        active = ct.Active,
        usesBikes = ct.UsesBikes  // <-- novo campo
    };

    public record CreateClassTypeRequest(string name, bool active = true, bool usesBikes = false);
    public record UpdateClassTypeRequest(string name, bool active, bool usesBikes = false);
}

public record ClassTypeRequest(string name, bool? active);