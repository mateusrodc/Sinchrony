using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Cards.Commands.AddCard;
using Sinchrony.Application.Cards.Commands.RemoveCard;
using Sinchrony.Application.Cards.Commands.SetDefaultCard;
using Sinchrony.Application.Cards.Queries.ListCards;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("cards")]
public class CardsController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await mediator.Send(new ListCardsQuery(UserId), ct);
        return Ok(new { data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddCardRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new AddCardCommand(UserId, req.number, req.holderName, req.expiryDate, req.cvv, req.nickname), ct);
        return StatusCode(201, result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await mediator.Send(new RemoveCardCommand(UserId, id), ct);
        return Ok(new { success = true });
    }

    [HttpPut("{id}/default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new SetDefaultCardCommand(UserId, id), ct);
        return Ok(new { data = result });
    }
}

public record AddCardRequest(string number, string holderName, string expiryDate, string cvv, string? nickname);