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
    int MaxDependents = 0) : IRequest<PackageDto>;

public class CreatePackageCommandHandler(IPackageRepository packageRepository)
    : IRequestHandler<CreatePackageCommand, PackageDto>
{
    public async Task<PackageDto> Handle(CreatePackageCommand request, CancellationToken ct)
    {
        var package = Package.Create(
            request.Name, request.Description, request.Credits,
            request.Price, request.ValidityDays, request.Popular,
            request.Active, request.DisplayOrder,
            request.PackageTypeId, request.PurchaseStrategy,
            request.MaxDependents);

        await packageRepository.AddAsync(package, ct);
        await packageRepository.SaveAsync(ct);

        return ListPackagesQueryHandler.MapToDto(package);
    }
}