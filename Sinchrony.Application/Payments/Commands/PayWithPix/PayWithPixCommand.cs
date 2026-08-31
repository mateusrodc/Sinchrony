using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Payments.Commands.PayWithPix;

public record PayWithPixCommand(
    Guid UserId, decimal Amount,
    List<Guid> PackageIds, string? CouponCode,
    string? Cpf = null)
    : IRequest<PixPaymentResponseDto>;

public record PixPaymentResponseDto(bool Success, string TransactionId, string PixCode, string QrCodeBase64);

public class PayWithPixCommandHandler(
    IUserRepository userRepository,
    IPackageRepository packageRepository,
    IPurchaseRepository purchaseRepository,
    ICouponRepository couponRepository,
    IAsaasService asaasService,
    IAuditService auditService) : IRequestHandler<PayWithPixCommand, PixPaymentResponseDto>
{
    public async Task<PixPaymentResponseDto> Handle(PayWithPixCommand request, CancellationToken ct)
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

        // Nunca confiar no "amount" que o cliente manda: recalcula a partir do preço real
        // dos pacotes + desconto do cupom, e é esse valor (não o do request) que é cobrado
        // e persistido. Sem isso, bastava mandar um "amount" baixo pra gerar uma cobrança
        // Pix de centavos e, quando confirmada pelo webhook, liberar os créditos do pacote
        // inteiro.
        var subtotal = packages.Sum(p => p.Price);
        var expectedAmount = coupon?.ApplyDiscount(subtotal) ?? subtotal;

        if (Math.Abs(expectedAmount - request.Amount) > 0.01m)
            throw DomainException.Validation("AMOUNT_MISMATCH",
                "O valor informado não corresponde ao preço dos pacotes selecionados.");

        // Usa CPF do request se informado, senão usa o do cadastro
        var cpf = request.Cpf ?? user.Cpf;

        var customerId = await asaasService.GetOrCreateCustomerAsync(
            user.Name, user.Email, cpf, ct);

        var result = await asaasService.CreatePixChargeAsync(
            customerId, expectedAmount, "4Sinchrony - Pacote de aulas", ct);

        // PIX: compra fica PENDING até confirmação via webhook
        foreach (var pkg in packages)
        {
            var purchase = Purchase.CreatePending(
                user.Id, pkg.Id, expectedAmount, "pix",
                result.TransactionId, coupon?.Id);
            await purchaseRepository.AddAsync(purchase, ct);
        }

        await purchaseRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "payment.pix_initiated", "Purchase",
            null, user.Id,
            $"TransactionId: {result.TransactionId}, Amount: {request.Amount}", ct: ct);

        return new PixPaymentResponseDto(true, result.TransactionId, result.PixCode, result.QrCodeBase64);
    }
}