using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.Extensions;
using Sinchrony.Application.Series;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.Erp;

[Authorize(Roles = "teacher,admin")]
[ApiController]
[Route("api/class-series")]
[Produces("application/json")]
public class ErpClassSeriesController(ClassSeriesService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [Authorize(Roles = "admin")]
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ClassSeriesRequest req, CancellationToken ct)
    {
        var result = await service.PreviewAsync(req.ToInput(), ct);
        return Ok(new { data = result });
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassSeriesRequest req, CancellationToken ct)
    {
        var result = await service.CreateAsync(req.ToInput(), req.requestId, UserId, ct);
        // requestId já usado: devolve a série existente sem gerar nada (200), como pede a idempotência.
        return StatusCode(result.IsNew ? 201 : 200, new { data = result.Data });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, UserId, User.IsInRole("admin"), ct);
        return Ok(new { data = result });
    }

    // "Esta e as próximas": from = SeriesDate da aula escolhida. "Toda a série": from = hoje.
    [Authorize(Roles = "admin")]
    [HttpPut("{id:guid}/occurrences")]
    public async Task<IActionResult> UpdateOccurrences(
        Guid id, [FromQuery] string? from, [FromBody] UpdateClassSeriesRequest req, CancellationToken ct)
    {
        var result = await service.UpdateOccurrencesAsync(
            id, DateParams.Require(from, "from"),
            new ClassSeriesChanges(req.name, req.classTypeId, req.teacherId, req.studioId,
                req.startTime, req.duration, req.totalSpots),
            UserId, ct);
        return Ok(new { data = result });
    }

    [Authorize(Roles = "admin")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] string? from, CancellationToken ct)
    {
        var result = await service.CancelAsync(id, DateParams.Require(from, "from"), UserId, ct);
        return Ok(new { data = result });
    }

    [Authorize(Roles = "admin")]
    [HttpPost("{id:guid}/extend")]
    public async Task<IActionResult> Extend(Guid id, [FromBody] ExtendClassSeriesRequest req, CancellationToken ct)
    {
        var result = await service.ExtendAsync(id, req.endDate, req.excludedDates, req.dryRun, UserId, ct);
        return Ok(new { data = (object?)result.Preview ?? result.Created });
    }
}

public record ClassSeriesRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string startTime, int duration, int totalSpots,
    List<int> daysOfWeek, string startDate, string endDate, List<string>? excludedDates = null)
{
    public ClassSeriesInput ToInput() => new(
        name, classTypeId, teacherId, studioId, startTime, duration, totalSpots,
        daysOfWeek, startDate, endDate, excludedDates);
}

public record CreateClassSeriesRequest(
    string name, Guid classTypeId, Guid teacherId, Guid studioId,
    string startTime, int duration, int totalSpots,
    List<int> daysOfWeek, string startDate, string endDate, List<string>? excludedDates, Guid requestId)
{
    public ClassSeriesInput ToInput() => new(
        name, classTypeId, teacherId, studioId, startTime, duration, totalSpots,
        daysOfWeek, startDate, endDate, excludedDates);
}

public record UpdateClassSeriesRequest(
    string? name, Guid? classTypeId, Guid? teacherId, Guid? studioId,
    string? startTime, int? duration, int? totalSpots);

public record ExtendClassSeriesRequest(string endDate, List<string>? excludedDates, bool dryRun = false);
