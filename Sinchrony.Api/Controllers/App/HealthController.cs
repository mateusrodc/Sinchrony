using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Infrastructure.Persistence;

namespace Sinchrony.Api.Controllers.App;

[ApiController]
[Route("health")]
public class HealthController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
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