using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Purchases.Queries.ListPurchases;

public record ListPurchasesQuery(Guid UserId) : IRequest<IEnumerable<PurchaseDto>>;

public record PurchaseDto(
    Guid Id,
    PurchasePackageDto Package,
    decimal Amount,
    CouponInfoDto? Coupon,
    string PaymentMethod,
    string Status,
    DateTime CreatedAt);

public record PurchasePackageDto(Guid Id, string Name, int Credits, decimal Price);
public record CouponInfoDto(string Code, decimal Discount, string DiscountType);

public class ListPurchasesQueryHandler(IPurchaseRepository purchaseRepository)
    : IRequestHandler<ListPurchasesQuery, IEnumerable<PurchaseDto>>
{
    public async Task<IEnumerable<PurchaseDto>> Handle(ListPurchasesQuery request, CancellationToken ct)
    {
        var purchases = await purchaseRepository.ListByUserAsync(request.UserId, ct);
        return purchases.Select(p => new PurchaseDto(
            p.Id,
            new PurchasePackageDto(p.Package!.Id, p.Package.Name, p.Package.Credits, p.Package.Price),
            p.Amount,
            p.Coupon is null ? null : new CouponInfoDto(p.Coupon.Code, p.Coupon.Discount, p.Coupon.DiscountType),
            p.PaymentMethod,
            p.Status,
            p.CreatedAt));
    }
}