using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Notifications.Commands.UpdateEmail;
using Sinchrony.Application.Notifications.Commands.UpdatePreference;
using Sinchrony.Application.Notifications.Commands.UpdatePush;
using Sinchrony.Application.Notifications.Queries.GetNotificationPreferences;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("notifications")]
public class NotificationsController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationPreferencesQuery(UserId), ct);
        return Ok(result);
    }

    [HttpPut("preferences/{id}")]
    public async Task<IActionResult> UpdatePreference(string id, [FromBody] UpdatePreferenceRequest req, CancellationToken ct)
    {
        await mediator.Send(new UpdatePreferenceCommand(UserId, id, req.enabled), ct);
        return Ok(new { success = true });
    }

    [HttpPut("push")]
    public async Task<IActionResult> UpdatePush([FromBody] UpdateToggleRequest req, CancellationToken ct)
    {
        await mediator.Send(new UpdatePushCommand(UserId, req.enabled), ct);
        return Ok(new { success = true });
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateToggleRequest req, CancellationToken ct)
    {
        await mediator.Send(new UpdateEmailCommand(UserId, req.enabled), ct);
        return Ok(new { success = true });
    }
}

public record UpdatePreferenceRequest(bool enabled);
public record UpdateToggleRequest(bool enabled);