using MediatR;
using Sinchrony.Application.Packages.Queries.ListPackages;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Packages.Commands.TogglePackage;

public record TogglePackageCommand(Guid Id) : IRequest<PackageDto>;

public class TogglePackageCommandHandler(IPackageRepository packageRepository)
    : IRequestHandler<TogglePackageCommand, PackageDto>
{
    public async Task<PackageDto> Handle(TogglePackageCommand request, CancellationToken ct)
    {
        var package = await packageRepository.GetByIdAsync(request.Id, ct)
            ?? throw DomainException.NotFound("Package not found.");

        package.Toggle();
        await packageRepository.SaveAsync(ct);
        return ListPackagesQueryHandler.MapToDto(package);
    }
}