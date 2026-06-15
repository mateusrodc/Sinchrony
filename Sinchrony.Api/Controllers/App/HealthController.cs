using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Api.SwaggerExamples.Health;
using Sinchrony.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.Filters;

namespace Sinchrony.Api.Controllers.App;

[ApiController]
[Route("health")]
public class HealthController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerResponseExample(200, typeof(HealthResponseExample))]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return Ok(new { status = "ok", db = "up" });
        }
        catch
        {
            return StatusCode(503, new { status = "error", db = "down" });
        }
    }
}