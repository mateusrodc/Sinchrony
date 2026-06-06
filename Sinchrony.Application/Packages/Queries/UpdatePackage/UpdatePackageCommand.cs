using MediatR;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Packages.Commands.UpdatePackage;

public record UpdatePackageCommand(
    Guid Id, string Name, string? Description, int Credits,
    decimal Price, int ValidityDays, bool Popular,
    bool Active, int DisplayOrder) : IRequest<PackageDto>;

public class UpdatePackageCommandHandler(IPackageRepository packageRepository)
    : IRequestHandler<UpdatePackageCommand, PackageDto>
{
    public async Task<PackageDto> Handle(UpdatePackageCommand request, CancellationToken ct)
    {
        var package = await packageRepository.GetByIdAsync(request.Id, ct)
            ?? throw DomainException.NotFound("Package not found.");

        package.Update(request.Name, request.Description, request.Credits,
            request.Price, request.ValidityDays, request.Popular,
            request.Active, request.DisplayOrder);

        await packageRepository.SaveAsync(ct);
        return ListPackagesQueryHandler.MapToDto(package);
    }
}