using MediatR;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Packages.Queries.ListPackages;

public record ListPackagesQuery(bool? ActiveOnly) : IRequest<IEnumerable<PackageDto>>;

public record PackageDto(
    Guid Id, string Name, string? Description,
    int Credits, decimal Price, decimal PricePerCredit,
    int ValidityDays, bool Popular, bool Active,
    int DisplayOrder, DateTime CreatedAt, DateTime UpdatedAt);

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
            p.ValidityDays, p.Popular, p.Active, p.DisplayOrder, p.CreatedAt, p.UpdatedAt);
}