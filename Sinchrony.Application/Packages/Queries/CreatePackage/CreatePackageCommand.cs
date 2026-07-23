using MediatR;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Packages.Commands.CreatePackage;

public record CreatePackageCommand(
    string Name, string? Description, int Credits,
    decimal Price, int ValidityDays, bool Popular,
    bool Active, int DisplayOrder,
    Guid? PackageTypeId = null,
    string PurchaseStrategy = "block",
    int MaxDependents = 0,
    int? CreditsPerMember = null,
    int? MaxFutureBookings = null,
    int? MaxBookingsPerDay = null,
    int? MaxBookingsPerWeek = null,
    int? MaxBookingsPerMonth = null,
    int? CancellationDeadlineHours = null,
    int? BookingWindowDays = null,
    int? EarlyAccessHours = null,
    bool? AllowWaitlist = null,
    int? WaitlistPriority = null,
    bool? ReschedulingAllowed = null,
    int? ReschedulingDeadlineHours = null,
    bool? NoShowCreditPenalty = null,
    int? MaxNoShowsBeforeBlock = null,
    int? NoShowBlockWindowDays = null,
    List<Guid>? BenefitIds = null) : IRequest<PackageDto>;

public class CreatePackageCommandHandler(
    IPackageRepository packageRepository,
    IBenefitRepository benefitRepository) : IRequestHandler<CreatePackageCommand, PackageDto>
{
    public async Task<PackageDto> Handle(CreatePackageCommand request, CancellationToken ct)
    {
        var package = Package.Create(
            request.Name, request.Description, request.Credits,
            request.Price, request.ValidityDays, request.Popular,
            request.Active, request.DisplayOrder,
            request.PackageTypeId, request.PurchaseStrategy,
            request.MaxDependents);

        // Aplica todas as regras específicas
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
            request.NoShowCreditPenalty ?? true, request.MaxNoShowsBeforeBlock,
            request.NoShowBlockWindowDays ?? 30);

        await packageRepository.AddAsync(package, ct);

        // Associa benefícios
        if (request.BenefitIds is not null && request.BenefitIds.Count > 0)
            await packageRepository.UpdateBenefitsAsync(package.Id, request.BenefitIds, ct);

        await packageRepository.SaveAsync(ct);

        return ListPackagesQueryHandler.MapToDto(package);
    }
}