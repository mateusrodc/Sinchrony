using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sinchrony.Application.Payments.Commands.PayWithCard;
using Sinchrony.Application.Payments.Commands.PayWithPix;
using System.Security.Claims;

namespace Sinchrony.Api.Controllers.App;

[Authorize]
[ApiController]
[Route("payments")]
[Produces("application/json")]
public class PaymentsController(IMediator mediator) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!);

    [HttpPost("validate-coupon")]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new Application.Payments.Queries.ValidateCoupon.ValidateCouponQuery(req.code), ct);
        if (result is null)
            return Ok(new { valid = false, coupon = (object?)null });
        return Ok(new { valid = true, coupon = result });
    }

    [HttpPost("pix")]
    public async Task<IActionResult> PayWithPix([FromBody] PixPaymentRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new PayWithPixCommand(UserId, req.amount, req.packageIds, req.couponCode), ct);
        return Ok(new
        {
            success = result.Success,
            transactionId = result.TransactionId,
            pixCode = result.PixCode,
            pixQRCode = result.QrCodeBase64
        });
    }

    [HttpPost("card")]
    public async Task<IActionResult> PayWithCard([FromBody] CardPaymentRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new PayWithCardCommand(UserId, req.amount, req.cardToken, req.packageIds, req.couponCode), ct);
        return Ok(new
        {
            success = result.Success,
            transactionId = result.TransactionId,
            message = result.Message
        });
    }
}

public record ValidateCouponRequest(string code);
public record PixPaymentRequest(decimal amount, List<Guid> packageIds, string? couponCode);
public record CardPaymentRequest(decimal amount, string cardToken, List<Guid> packageIds, string? couponCode);