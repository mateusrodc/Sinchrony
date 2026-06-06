using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Payments.Queries.ValidateCoupon;

public record ValidateCouponQuery(string Code) : IRequest<CouponDto?>;

public record CouponDto(string Code, decimal Discount, string DiscountType);

public class ValidateCouponQueryHandler(ICouponRepository couponRepository)
    : IRequestHandler<ValidateCouponQuery, CouponDto?>
{
    public async Task<CouponDto?> Handle(ValidateCouponQuery request, CancellationToken ct)
    {
        var coupon = await couponRepository.GetByCodeAsync(request.Code, ct);
        if (coupon is null || !coupon.IsValid()) return null;
        return new CouponDto(coupon.Code, coupon.Discount, coupon.DiscountType);
    }
}