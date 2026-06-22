using MediatR;
using Sinchrony.Application.Common;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Purchases.Queries.ListPurchases;

public record ListPurchasesQuery(
    Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<PurchaseDto>>;

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
    : IRequestHandler<ListPurchasesQuery, PagedResult<PurchaseDto>>
{
    public async Task<PagedResult<PurchaseDto>> Handle(ListPurchasesQuery request, CancellationToken ct)
    {
        var (items, total) = await purchaseRepository.ListByUserPagedAsync(
            request.UserId, request.Page, request.PageSize, ct);

        var data = items.Select(p => new PurchaseDto(
            p.Id,
            new PurchasePackageDto(p.Package!.Id, p.Package.Name, p.Package.Credits, p.Package.Price),
            p.Amount,
            p.Coupon is null ? null : new CouponInfoDto(p.Coupon.Code, p.Coupon.Discount, p.Coupon.DiscountType),
            p.PaymentMethod, p.Status, p.CreatedAt));

        return PagedResult.Create(data, request.Page, request.PageSize, total);
    }
}