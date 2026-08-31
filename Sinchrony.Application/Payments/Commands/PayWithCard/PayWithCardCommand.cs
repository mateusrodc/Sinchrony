using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Payments.Commands.PayWithCard;

public record PayWithCardCommand(
    Guid UserId, decimal Amount, string CardToken,
    List<Guid> PackageIds, string? CouponCode)
    : IRequest<CardPaymentResponseDto>;

public record CardPaymentResponseDto(bool Success, string TransactionId, string Message);

public class PayWithCardCommandHandler(
    IUserRepository userRepository,
    IPackageRepository packageRepository,
    IPurchaseRepository purchaseRepository,
    ICouponRepository couponRepository,
    ICreditTransactionRepository creditTransactionRepository,
    ICardRepository cardRepository,
    IAsaasService asaasService,
    IAuditService auditService) : IRequestHandler<PayWithCardCommand, CardPaymentResponseDto>
{
    public async Task<CardPaymentResponseDto> Handle(PayWithCardCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        // O token do cartão é exibido pro app (pra montar esse payload), mas isso não
        // significa que qualquer token vale — sem essa checagem, um usuário poderia mandar
        // o token de OUTRO cliente e cobrar o cartão dele em nome da própria compra.
        var ownsCard = await cardRepository.ExistsByTokenAsync(request.UserId, request.CardToken, ct);
        if (!ownsCard)
            throw DomainException.Forbidden("Este cartão não pertence a você.");

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
        // e persistido. Sem isso, bastava mandar um "amount" baixo pra pagar centavos e
        // receber os créditos do pacote inteiro.
        var subtotal = packages.Sum(p => p.Price);
        var expectedAmount = coupon?.ApplyDiscount(subtotal) ?? subtotal;

        if (Math.Abs(expectedAmount - request.Amount) > 0.01m)
            throw DomainException.Validation("AMOUNT_MISMATCH",
                "O valor informado não corresponde ao preço dos pacotes selecionados.");

        var customerId = await asaasService.GetOrCreateCustomerAsync(
            user.Name, user.Email, user.Cpf, ct);

        var result = await asaasService.ChargeCardAsync(
            customerId, request.CardToken, expectedAmount,
            "4Sinchrony - Pacote de aulas", ct);

        // Cartão: aprovação síncrona — credita imediatamente
        var totalCredits = packages.Sum(p => p.Credits);
        user.AddCredits(totalCredits);

        var creditTx = CreditTransaction.Create(
            user.Id, totalCredits, user.Credits,
            $"Card purchase confirmed: {result.TransactionId}",
            "purchase", null);
        await creditTransactionRepository.AddAsync(creditTx, ct);

        foreach (var pkg in packages)
        {
            var purchase = Purchase.Create(
                user.Id, pkg.Id, expectedAmount, "card",
                result.TransactionId, coupon?.Id);
            await purchaseRepository.AddAsync(purchase, ct);
        }

        await userRepository.SaveAsync(ct);
        await creditTransactionRepository.SaveAsync(ct);
        await purchaseRepository.SaveAsync(ct);

        await auditService.LogAsync(
            "payment.card_confirmed", "Purchase",
            null, user.Id,
            $"TransactionId: {result.TransactionId}, Amount: {request.Amount}",
            ct: ct);

        return new CardPaymentResponseDto(true, result.TransactionId, result.Message);
    }
}