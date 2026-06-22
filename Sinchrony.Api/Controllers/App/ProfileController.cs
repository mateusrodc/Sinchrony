using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Profile.Commands.UpdateProfile;
using Sinchrony.Application.Profile.Queries.ProfileProgress;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("profile")]
[Produces("application/json")]
public class ProfileController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateProfileCommand(UserId, req.name, req.email, req.phone, req.avatar), ct);
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<IActionResult> Progress(CancellationToken ct)
    {
        var result = await mediator.Send(new ProfileProgressQuery(UserId), ct);
        return Ok(result);
    }
}

public record UpdateProfileRequest(string name, string email, string? phone, string? avatar);