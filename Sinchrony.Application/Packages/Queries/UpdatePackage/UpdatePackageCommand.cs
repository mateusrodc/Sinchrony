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
    List<Guid> BenefitIds) : IRequest<PackageDto>;

public class UpdatePackageCommandHandler(
    IPackageRepository packageRepository,
    IBenefitRepository benefitRepository) : IRequestHandler<UpdatePackageCommand, PackageDto>
{
    public async Task<PackageDto> Handle(UpdatePackageCommand request, CancellationToken ct)
    {
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
            request.NoShowBlockWindowDays);

        // Atualiza benefícios — substitui tudo a cada update
        await packageRepository.UpdateBenefitsAsync(request.Id, request.BenefitIds, ct);

        await packageRepository.SaveAsync(ct);
        return ListPackagesQueryHandler.MapToDto(package);
    }
}