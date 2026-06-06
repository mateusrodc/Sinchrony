using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Referrals;
using Sinchrony.Application.Referrals.Queries.GetReferral;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("referral")]
public class ReferralsController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [SwaggerResponseExample(200, typeof(ReferralResponseExample))]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await mediator.Send(new GetReferralQuery(UserId), ct);
        return Ok(result);
    }

    [HttpGet("achievements")]
    public IActionResult Achievements()
    {
        // Estrutura base — expanda conforme regras de negócio futuras
        return Ok(new
        {
            data = new[]
            {
                new { id = "ach_1", title = "Primeira Indicação", unlocked = false, progress = 0 },
                new { id = "ach_2", title = "5 Indicações",       unlocked = false, progress = 0 },
                new { id = "ach_3", title = "10 Indicações",      unlocked = false, progress = 0 }
            }
        });
    }
}