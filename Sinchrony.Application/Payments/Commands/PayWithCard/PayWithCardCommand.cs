using MediatR;
using Microsoft.Extensions.Logging;
using Sinchrony.Application.Payments.Commands;
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
    IAuditService auditService,
    PurchasePackageService purchaseService,
    ILogger<PayWithCardCommandHandler> logger,
    OncePerStudentGuard oncePerStudentGuard) : IRequestHandler<PayWithCardCommand, CardPaymentResponseDto>
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

            if (!pkg.Active)
                throw DomainException.Validation("PACKAGE_INACTIVE", "Package is not available.");

            // Plano recorrente precisa criar uma assinatura na Asaas (POST /subscriptions),
            // não uma cobrança avulsa — sem essa checagem, um App antigo ou um POST direto
            // consegue "comprar" o plano sem nunca gerar a assinatura, e ele nunca renova.
            if (pkg.IsRecurring)
                throw DomainException.Validation("RECURRING_REQUIRES_SUBSCRIPTION",
                    "Este plano tem renovação automática. Atualize o aplicativo para contratá-lo.");

            if (!pkg.AllowsCard)
                throw DomainException.Validation("PAYMENT_METHOD_NOT_ALLOWED",
                    "Este pacote não aceita pagamento via cartão.");

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

        // Pacote de compra única: checagem + cobrança sob trava por família+pacote. Um PIX em
        // aberto do mesmo pacote bloqueia o cartão (não dá pra devolver um PIX aqui).
        return await oncePerStudentGuard.RunLockedAsync(user.Id, packages, async () =>
        {
            await oncePerStudentGuard.CheckAsync(user.Id, packages, PurchaseOrigin.AppCard, ct);

            var customerId = await asaasService.GetOrCreateCustomerAsync(
                AsaasCustomerData.From(user), ct);

            var result = await asaasService.ChargeCardAsync(
                customerId, request.CardToken, expectedAmount,
                "4Sinchrony - Pacote de aulas", ct);

            if (result.Status is "CONFIRMED" or "RECEIVED")
            {
                // Cartão aprovado de forma síncrona — cria o pacote do aluno e credita na hora. O pacote
                // real (plano) entra antes da Aula Avulsa, para a avulsa não ocupar o lugar de pacote ativo.
                var parts = PurchaseAmountSplitter.Split(packages, expectedAmount);
                var totalCredits = 0;
                foreach (var pkg in packages.OrderBy(p => p.IsSingleClass))
                {
                    var grant = await purchaseService.GrantCartPackageAsync(user.Id, pkg, ct);
                    totalCredits += grant.CreditsAdded;
                    if (grant.FellBack)
                        logger.LogWarning(
                            "Compra no cartão {Tx}: pacote {Package} não pôde ser ativado ({Strategy}); só os créditos foram concedidos.",
                            result.TransactionId, pkg.Name, pkg.PurchaseStrategy);
                }

                if (totalCredits > 0)
                {
                    var creditTx = CreditTransaction.Create(
                        user.Id, totalCredits, user.Credits,
                        $"Card purchase confirmed: {result.TransactionId}",
                        "purchase", null);
                    await creditTransactionRepository.AddAsync(creditTx, ct);
                }

                for (var i = 0; i < packages.Count; i++)
                {
                    var purchase = Purchase.Create(
                        user.Id, packages[i].Id, parts[i], "card",
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
            }
            else
            {
                // PENDING: aguardando análise antifraude — créditos só são concedidos quando o
                // webhook confirmar (PAYMENT_CONFIRMED/PAYMENT_RECEIVED), evitando dar créditos
                // por um cartão que a Asaas ainda pode reprovar.
                var pendingParts = PurchaseAmountSplitter.Split(packages, expectedAmount);
                for (var i = 0; i < packages.Count; i++)
                {
                    var purchase = Purchase.CreatePending(
                        user.Id, packages[i].Id, pendingParts[i], "card",
                        result.TransactionId, coupon?.Id);
                    await purchaseRepository.AddAsync(purchase, ct);
                }

                await purchaseRepository.SaveAsync(ct);

                await auditService.LogAsync(
                    "payment.card_pending", "Purchase",
                    null, user.Id,
                    $"TransactionId: {result.TransactionId}, Amount: {request.Amount}",
                    ct: ct);
            }


            return new CardPaymentResponseDto(result.Status is "CONFIRMED" or "RECEIVED", result.TransactionId, result.Message);
        }, ct);
    }
}
