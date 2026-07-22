using MediatR;
using Sinchrony.Application.Payments.Commands;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Application.Packages.Commands.PurchasePackage;

public record PurchasePackageCommand(
    Guid UserId, Guid PackageId, string PaymentMethod,
    decimal Amount, string? CardToken, string? Cpf,
    string? CouponCode) : IRequest<StudentPackageResultDto>;

public record StudentPackageResultDto(
    Guid Id, Guid PackageId, string PackageName,
    string Status, DateTime StartDate, DateTime EndDate,
    string TransactionId, string PaymentMethod);

public class PurchasePackageCommandHandler(
    IUserRepository userRepository,
    IPackageRepository packageRepository,
    ICouponRepository couponRepository,
    IPurchaseRepository purchaseRepository,
    IStudentPackageRepository studentPackageRepository,
    IDependentPackageAllocationRepository allocationRepository,
    IDependentRepository dependentRepository,
    IAsaasService asaasService,
    IAuditService auditService) : IRequestHandler<PurchasePackageCommand, StudentPackageResultDto>
{
    public async Task<StudentPackageResultDto> Handle(PurchasePackageCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var package = await packageRepository.GetByIdAsync(request.PackageId, ct)
            ?? throw DomainException.NotFound("Package not found.");

        if (!package.Active)
            throw DomainException.Validation("PACKAGE_INACTIVE", "Package is not available.");

        Coupon? coupon = null;
        if (!string.IsNullOrEmpty(request.CouponCode))
        {
            coupon = await couponRepository.GetByCodeAsync(request.CouponCode, ct);
            if (coupon is null || !coupon.IsValid())
                throw DomainException.Validation("INVALID_COUPON", "Invalid or expired coupon.");
        }

        // Verifica estratégia antes de processar pagamento
        var active = await studentPackageRepository.GetActiveByStudentAsync(request.UserId, ct);
        if (active is not null && package.PurchaseStrategy == "block")
            throw DomainException.Conflict("ACTIVE_PACKAGE_EXISTS",
                "Você já possui um pacote ativo. Este pacote não permite compra com pacote ativo.");

        // Processa pagamento via Asaas
        var cpf = request.Cpf ?? user.Cpf;
        var customerId = await asaasService.GetOrCreateCustomerAsync(
            user.Name, user.Email, cpf, ct);

        string transactionId;

        if (request.PaymentMethod == "pix")
        {
            if (string.IsNullOrEmpty(cpf))
                throw DomainException.Validation("CPF_REQUIRED", "CPF é obrigatório para pagamento via PIX.");

            var pixResult = await asaasService.CreatePixChargeAsync(
                customerId, request.Amount, $"4Sinchrony - {package.Name}", ct);
            transactionId = pixResult.TransactionId;
        }
        else if (request.PaymentMethod == "card")
        {
            if (string.IsNullOrEmpty(request.CardToken))
                throw DomainException.Validation("CARD_TOKEN_REQUIRED", "Token do cartão é obrigatório.");

            var cardResult = await asaasService.ChargeCardAsync(
                customerId, request.CardToken, request.Amount,
                $"4Sinchrony - {package.Name}", ct);
            transactionId = cardResult.TransactionId;
        }
        else
        {
            throw DomainException.Validation("INVALID_PAYMENT_METHOD", "Método de pagamento inválido.");
        }

        // Cria Purchase
        var purchase = Purchase.CreatePending(
            user.Id, package.Id, request.Amount,
            request.PaymentMethod, transactionId, coupon?.Id);
        await purchaseRepository.AddAsync(purchase, ct);

        // Aplica estratégia e cria StudentPackage
        var purchaseService = new PurchasePackageService(
            studentPackageRepository, allocationRepository, dependentRepository);

        StudentPackage? studentPackage = null;

        if (request.PaymentMethod == "card")
        {
            // Cartão: aprovação síncrona — ativa imediatamente
            purchase.Confirm();
            studentPackage = await purchaseService.ProcessAndReturnAsync(
                request.UserId, package, ct);
        }
        else
        {
            // PIX: pendente — StudentPackage criado após webhook
            studentPackage = StudentPackage.CreateQueued(
                request.UserId, package.Id, package.ValidityDays);
            await studentPackageRepository.AddAsync(studentPackage, ct);
        }

        await purchaseRepository.SaveAsync(ct);
        await studentPackageRepository.SaveAsync(ct);

        await auditService.LogAsync("package.purchased", "Purchase",
            purchase.Id, user.Id,
            $"Package: {package.Name}, Method: {request.PaymentMethod}", ct: ct);

        return new StudentPackageResultDto(
            studentPackage.Id, package.Id, package.Name,
            studentPackage.Status.ToString(),
            studentPackage.StartDate, studentPackage.EndDate,
            transactionId, request.PaymentMethod);
    }
}