using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Purchases;
using Sinchrony.Application.Purchases.Queries.ListPurchases;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("purchases")]
public class PurchasesController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [SwaggerResponseExample(200, typeof(PurchaseListResponseExample))]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await mediator.Send(new ListPurchasesQuery(UserId), ct);
        return Ok(new { data = result });
    }
}