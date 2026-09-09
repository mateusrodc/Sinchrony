using MediatR;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Packages.Commands.UpdatePackage;

public record UpdatePackageCommand(
    Guid Id, string Name, string? Description, int Credits,
    decimal Price, int ValidityDays, bool Popular,
    bool Active, int DisplayOrder,
    Guid PackageTypeId,
    string PurchaseStrategy,
    int MaxDependents,
    int? CreditsPerMember,
    int? MaxFutureBookings,
    int? MaxBookingsPerDay,
    int? MaxBookingsPerWeek,
    int? MaxBookingsPerMonth,
    int? CancellationDeadlineHours,
    int? BookingWindowDays,
    int? EarlyAccessHours,
    bool? AllowWaitlist,
    int? WaitlistPriority,
    bool? ReschedulingAllowed,
    int? ReschedulingDeadlineHours,
    bool NoShowCreditPenalty,
    int? MaxNoShowsBeforeBlock,
    int NoShowBlockWindowDays,
    List<Guid> BenefitIds,
    bool AllowsPix = true,
    bool AllowsCard = true,
    bool AllowsInstallments = true,
    int? MaxInstallments = null) : IRequest<PackageDto>;

public class UpdatePackageCommandHandler(
    IPackageRepository packageRepository,
    IBenefitRepository benefitRepository) : IRequestHandler<UpdatePackageCommand, PackageDto>
{
    public async Task<PackageDto> Handle(UpdatePackageCommand request, CancellationToken ct)
    {
        // CreditsPerMember só faz sentido para pacotes com dependentes (família). Em pacote
        // individual o valor concedido é sempre `Credits` — permitir CreditsPerMember aqui
        // deixaria um valor "fantasma" no banco que passaria a ser usado silenciosamente na
        // concessão de créditos assim que MaxDependents fosse alterado no futuro.
        if (request.MaxDependents <= 0 && request.CreditsPerMember.HasValue)
            throw DomainException.Validation("CREDITS_PER_MEMBER_NOT_ALLOWED",
                "CreditsPerMember só pode ser definido em pacotes com dependentes (MaxDependents > 0).");

        var package = await packageRepository.GetByIdAsync(request.Id, ct)
            ?? throw DomainException.NotFound("Package not found.");

        package.UpdateFull(
            request.Name, request.Description, request.Credits,
            request.Price, request.ValidityDays, request.Popular,
            request.Active, request.DisplayOrder,
            request.PackageTypeId, request.PurchaseStrategy,
            request.MaxDependents, request.CreditsPerMember,
            request.MaxFutureBookings, request.MaxBookingsPerDay,
            request.MaxBookingsPerWeek, request.MaxBookingsPerMonth,
            request.CancellationDeadlineHours, request.BookingWindowDays,
            request.EarlyAccessHours, request.AllowWaitlist, request.WaitlistPriority,
            request.ReschedulingAllowed, request.ReschedulingDeadlineHours,
            request.NoShowCreditPenalty, request.MaxNoShowsBeforeBlock,
            request.NoShowBlockWindowDays,
            request.AllowsPix, request.AllowsCard,
            request.AllowsInstallments, request.MaxInstallments);

        await packageRepository.UpdateBenefitsAsync(request.Id, request.BenefitIds, ct);
        await packageRepository.SaveAsync(ct);

        // Recarrega para garantir navigation properties atualizados
        var updated = await packageRepository.GetByIdAsync(request.Id, ct);
        return ListPackagesQueryHandler.MapToDto(updated!);
    }
}