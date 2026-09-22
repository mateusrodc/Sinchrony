using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Payments.Commands;

// Pacote afetado pela estratégia de compra e quantos créditos foram de fato somados ao saldo
// (0 quando o pacote entrou na fila ou só estendeu a validade).
public record PackageGrantResult(StudentPackage? StudentPackage, int CreditsAdded);

public class PurchasePackageService(
    IStudentPackageRepository studentPackageRepository,
    IDependentPackageAllocationRepository allocationRepository,
    IDependentRepository dependentRepository,
    IUserRepository userRepository)
{
    public async Task<PackageGrantResult> ProcessAsync(
        Guid studentId, Package package,
        string source = "purchase",
        CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(studentId, ct);
        return await ProcessAndCreditAsync(studentId, package, user, source, ct);
    }

    public async Task<StudentPackage> ProcessAndReturnAsync(
        Guid studentId, Package package,
        string source = "purchase",
        CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(studentId, ct);
        await ProcessAndCreditAsync(studentId, package, user, source, ct);
        return await studentPackageRepository.GetActiveByStudentAsync(studentId, ct)
            ?? throw new InvalidOperationException("StudentPackage not created.");
    }

    private async Task<PackageGrantResult> ProcessAndCreditAsync(
        Guid studentId, Package package, User? user,
        string source, CancellationToken ct)
    {
        var active = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);
        var credits = package.GetCreditsToGrant();
        StudentPackage? affected = active;
        var creditsAdded = 0;

        if (active is not null)
        {
            // Aula Avulsa ativa não bloqueia nem enfileira um plano real: o plano ativa na hora,
            // seja qual for a estratégia dele. O crédito que sobrou da avulsa é mantido no saldo.
            var strategy = package.ReplacesActive(active.Package)
                ? "activate_immediately"
                : package.PurchaseStrategy;

            switch (strategy)
            {
                case "block":
                    throw DomainException.Conflict("ACTIVE_PACKAGE_EXISTS",
                        "Você já possui um pacote ativo.");

                case "queue":
                    var queued = StudentPackage.CreateQueued(studentId, package.Id, package.ValidityDays);
                    queued.SetSource(source, 0); // queued não credita ainda
                    await studentPackageRepository.AddAsync(queued, ct);
                    affected = queued;
                    break;

                case "sum_credits":
                    var titularAlloc = await allocationRepository
                        .GetByStudentPackageAndDependentAsync(active.Id, null, ct);
                    titularAlloc?.Credit(credits);
                    if (user is not null)
                    {
                        user.AddCredits(credits);
                        creditsAdded = credits;
                    }
                    break;

                case "sum_validity":
                    active.ExtendValidity(package.ValidityDays);
                    break;

                case "activate_immediately":
                    active.Cancel();
                    var newSp = StudentPackage.Create(studentId, package.Id, package.ValidityDays);
                    newSp.SetSource(source, credits);
                    await studentPackageRepository.AddAsync(newSp, ct);
                    await CreateAllocationsAsync(newSp, package, studentId, ct);
                    if (user is not null)
                    {
                        user.AddCredits(credits);
                        creditsAdded = credits;
                    }
                    affected = newSp;
                    break;
            }
        }
        else
        {
            var sp = StudentPackage.Create(studentId, package.Id, package.ValidityDays);
            sp.SetSource(source, credits);
            await studentPackageRepository.AddAsync(sp, ct);
            await CreateAllocationsAsync(sp, package, studentId, ct);
            affected = sp;

            if (user is not null)
            {
                user.AddCredits(credits);
                creditsAdded = credits;
                await userRepository.SaveAsync(ct);
            }
        }

        await studentPackageRepository.SaveAsync(ct);
        return new PackageGrantResult(affected, creditsAdded);
    }

    private async Task CreateAllocationsAsync(
        StudentPackage sp, Package package, Guid studentId, CancellationToken ct)
    {
        var dependents = (await dependentRepository.ListByStudentAsync(studentId, ct))
            .Where(d => d.Active).ToList();

        var totalPersons = 1 + dependents.Count;
        var creditsPerPerson = package.GetCreditsPerPerson(totalPersons);

        var titularAlloc = DependentPackageAllocation.Create(sp.Id, null, creditsPerPerson);
        await allocationRepository.AddAsync(titularAlloc, ct);

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