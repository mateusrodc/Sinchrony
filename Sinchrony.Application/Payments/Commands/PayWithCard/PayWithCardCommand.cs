using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Payments.Commands.PayWithCard;

public record PayWithCardCommand(Guid UserId, decimal Amount, string CardToken, List<Guid> PackageIds, string? CouponCode)
    : IRequest<CardPaymentResponseDto>;

public record CardPaymentResponseDto(bool Success, string TransactionId, string Message);

public class PayWithCardCommandHandler(
    IUserRepository userRepository,
    IPackageRepository packageRepository,
    IPurchaseRepository purchaseRepository,
    ICouponRepository couponRepository,
    IAsaasService asaasService) : IRequestHandler<PayWithCardCommand, CardPaymentResponseDto>
{
    public async Task<CardPaymentResponseDto> Handle(PayWithCardCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        Coupon? coupon = null;
        if (!string.IsNullOrEmpty(request.CouponCode))
        {
            coupon = await couponRepository.GetByCodeAsync(request.CouponCode, ct);
            if (coupon is null || !coupon.IsValid())
                throw DomainException.Validation("INVALID_COUPON", "Invalid or expired coupon.");
        }

        var packages = new List<Package>();
        foreach (var pkgId in request.PackageIds)
        {
            var pkg = await packageRepository.GetByIdAsync(pkgId, ct)
                ?? throw DomainException.NotFound($"Package {pkgId} not found.");
            packages.Add(pkg);
        }

        var customerId = await asaasService.GetOrCreateCustomerAsync(user.Name, user.Email, ct: ct);
        var result = await asaasService.ChargeCardAsync(customerId, request.CardToken, request.Amount, "4Sinchrony - Pacote de aulas", ct);

        var totalCredits = packages.Sum(p => p.Credits);
        user.AddCredits(totalCredits);

        foreach (var pkg in packages)
        {
            var purchase = Purchase.Create(user.Id, pkg.Id, request.Amount, "card", result.TransactionId, coupon?.Id);
            await purchaseRepository.AddAsync(purchase, ct);
        }

        await userRepository.SaveAsync(ct);
        await purchaseRepository.SaveAsync(ct);

        return new CardPaymentResponseDto(true, result.TransactionId, result.Message);
    }
}