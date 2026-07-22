using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Payments.Commands;

public class PurchasePackageService(
    IStudentPackageRepository studentPackageRepository,
    IDependentPackageAllocationRepository allocationRepository,
    IDependentRepository dependentRepository)
{
    public async Task ProcessAsync(
        Guid studentId, Package package, CancellationToken ct)
    {
        var active = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);

        if (active is not null)
        {
            switch (package.PurchaseStrategy)
            {
                case "block":
                    throw DomainException.Conflict("ACTIVE_PACKAGE_EXISTS",
                        "Você já possui um pacote ativo.");

                case "queue":
                    var queued = StudentPackage.CreateQueued(studentId, package.Id, package.ValidityDays);
                    await studentPackageRepository.AddAsync(queued, ct);
                    break;

                case "sum_credits":
                    var titularAlloc = await allocationRepository
                        .GetByStudentPackageAndDependentAsync(active.Id, null, ct);
                    var credits = package.CreditsPerMember ?? package.Credits;
                    titularAlloc?.Credit(credits);
                    break;

                case "sum_validity":
                    active.ExtendValidity(package.ValidityDays);
                    break;

                case "activate_immediately":
                    active.Cancel();
                    var newSp = StudentPackage.Create(studentId, package.Id, package.ValidityDays);
                    await studentPackageRepository.AddAsync(newSp, ct);
                    await CreateAllocationsAsync(newSp, package, studentId, ct);
                    break;
            }
        }
        else
        {
            var sp = StudentPackage.Create(studentId, package.Id, package.ValidityDays);
            await studentPackageRepository.AddAsync(sp, ct);
            await CreateAllocationsAsync(sp, package, studentId, ct);
        }

        await studentPackageRepository.SaveAsync(ct);
    }
    public async Task<StudentPackage> ProcessAndReturnAsync(
    Guid studentId, Package package, CancellationToken ct)
    {
        await ProcessAsync(studentId, package, ct);
        return await studentPackageRepository.GetActiveByStudentAsync(studentId, ct)
            ?? throw new InvalidOperationException("StudentPackage not created.");
    }

    private async Task CreateAllocationsAsync(
        StudentPackage sp, Package package, Guid studentId, CancellationToken ct)
    {
        var dependents = (await dependentRepository.ListByStudentAsync(studentId, ct))
            .Where(d => d.Active).ToList();

        var totalPersons = 1 + dependents.Count;
        var creditsPerPerson = package.CreditsPerMember
            ?? package.Credits / totalPersons;

        // Alocação do titular
        var titularAlloc = DependentPackageAllocation.Create(sp.Id, null, creditsPerPerson);
        await allocationRepository.AddAsync(titularAlloc, ct);

        // Alocação por dependente (só se pacote família)
        if (package.MaxDependents > 0)
        {
            foreach (var dep in dependents)
            {
                var depAlloc = DependentPackageAllocation.Create(sp.Id, dep.Id, creditsPerPerson);
                await allocationRepository.AddAsync(depAlloc, ct);
            }
        }
    }
}