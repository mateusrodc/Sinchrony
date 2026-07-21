using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Packages.Queries.ListPackages;

public record PackageDto(
    Guid Id, string Name, string? Description,
    int Credits, decimal Price, decimal PricePerCredit,
    int ValidityDays, bool Popular, bool Active,
    int DisplayOrder, DateTime CreatedAt, DateTime UpdatedAt,
    // Campos novos
    Guid PackageTypeId, string? PackageTypeName, bool IsFamily,
    string PurchaseStrategy, int MaxDependents, int? CreditsPerMember,
    int? MaxFutureBookings, int? MaxBookingsPerDay,
    int? MaxBookingsPerWeek, int? MaxBookingsPerMonth,
    int? CancellationDeadlineHours, int? BookingWindowDays,
    int? EarlyAccessHours, bool? AllowWaitlist, int? WaitlistPriority,
    bool? ReschedulingAllowed, int? ReschedulingDeadlineHours,
    bool NoShowCreditPenalty, int? MaxNoShowsBeforeBlock, int NoShowBlockWindowDays,
    List<BenefitDto> Benefits);

public record BenefitDto(Guid Id, string Name, string? Description, string? Icon);

public record ListPackagesQuery(bool? ActiveOnly) : IRequest<IEnumerable<PackageDto>>;

public class ListPackagesQueryHandler(IPackageRepository packageRepository)
    : IRequestHandler<ListPackagesQuery, IEnumerable<PackageDto>>
{
    public async Task<IEnumerable<PackageDto>> Handle(ListPackagesQuery request, CancellationToken ct)
    {
        var packages = await packageRepository.ListAsync(request.ActiveOnly, ct);
        return packages.Select(MapToDto);
    }

    public static PackageDto MapToDto(Domain.Entities.Package p) =>
        new(p.Id, p.Name, p.Description, p.Credits, p.Price, p.PricePerCredit,
            p.ValidityDays, p.Popular, p.Active, p.DisplayOrder, p.CreatedAt, p.UpdatedAt,
            p.PackageTypeId, p.PackageType?.Name, p.PackageType?.IsFamily ?? false,
            p.PurchaseStrategy, p.MaxDependents, p.CreditsPerMember,
            p.MaxFutureBookings, p.MaxBookingsPerDay,
            p.MaxBookingsPerWeek, p.MaxBookingsPerMonth,
            p.CancellationDeadlineHours, p.BookingWindowDays,
            p.EarlyAccessHours, p.AllowWaitlist, p.WaitlistPriority,
            p.ReschedulingAllowed, p.ReschedulingDeadlineHours,
            p.NoShowCreditPenalty, p.MaxNoShowsBeforeBlock, p.NoShowBlockWindowDays,
            p.PackageBenefits.Select(pb => new BenefitDto(
                pb.BenefitId,
                pb.Benefit?.Name ?? string.Empty,
                pb.Benefit?.Description,
                pb.Benefit?.Icon)).ToList());
}