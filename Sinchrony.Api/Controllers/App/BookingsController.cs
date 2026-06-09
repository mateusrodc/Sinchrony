using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Api.SwaggerExamples.Bookings;
using Sinchrony.Application.Bookings.Commands.CreateBooking;
using Sinchrony.Application.Bookings.Queries.ListBookings;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("bookings")]
public class BookingsController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpGet]
    [SwaggerResponseExample(200, typeof(BookingListResponseExample))]
    public async Task<IActionResult> List(
    [FromQuery] string? status,
    [FromQuery] bool history = false,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new ListBookingsQuery(UserId, status, history, page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateBookingCommand(UserId, req.classId, req.bikeNumber), ct);
        return StatusCode(201, result);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await mediator.Send(
            new Application.Bookings.Commands.CancelBooking.CancelBookingCommand(UserId, id), ct);
        return Ok(new { success = true });
    }
}

public record CreateBookingRequest(Guid classId, int? bikeNumber);