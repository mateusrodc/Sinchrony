using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Payments.Commands;

public class PurchasePackageService(
    IStudentPackageRepository studentPackageRepository,
    IDependentPackageAllocationRepository allocationRepository,
    IDependentRepository dependentRepository,
    IUserRepository userRepository,
    ICreditTransactionRepository creditTransactionRepository)
{
    public async Task ProcessAsync(
        Guid studentId, Package package, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(studentId, ct);
        await ProcessAndCreditAsync(studentId, package, user, null, ct);
    }

    public async Task<StudentPackage> ProcessAndReturnAsync(
        Guid studentId, Package package, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(studentId, ct);
        await ProcessAndCreditAsync(studentId, package, user, null, ct);
        return await studentPackageRepository.GetActiveByStudentAsync(studentId, ct)
            ?? throw new InvalidOperationException("StudentPackage not created.");
    }

    private async Task ProcessAndCreditAsync(
        Guid studentId, Package package, User? user,
        string? transactionRef, CancellationToken ct)
    {
        var active = await studentPackageRepository.GetActiveByStudentAsync(studentId, ct);

        StudentPackage? sp = null;

        if (active is not null)
        {
            switch (package.PurchaseStrategy)
            {
                case "block":
                    throw new InvalidOperationException("Active package exists.");

                case "queue":
                    var queued = StudentPackage.CreateQueued(studentId, package.Id, package.ValidityDays);
                    await studentPackageRepository.AddAsync(queued, ct);
                    // Não credita créditos — pacote está na fila
                    break;

                case "sum_credits":
                    var titularAlloc = await allocationRepository
                        .GetByStudentPackageAndDependentAsync(active.Id, null, ct);
                    var sumCredits = package.CreditsPerMember ?? package.Credits;
                    titularAlloc?.Credit(sumCredits);
                    // Credita no User.Credits também
                    if (user is not null)
                    {
                        user.AddCredits(sumCredits);
                        await CreditTransactionAsync(user, sumCredits, transactionRef, ct);
                    }
                    break;

                case "sum_validity":
                    active.ExtendValidity(package.ValidityDays);
                    break;

                case "activate_immediately":
                    active.Cancel();
                    sp = StudentPackage.Create(studentId, package.Id, package.ValidityDays);
                    await studentPackageRepository.AddAsync(sp, ct);
                    await CreateAllocationsAsync(sp, package, studentId, ct);
                    // Credita no User.Credits
                    if (user is not null)
                    {
                        var credits = package.CreditsPerMember ?? package.Credits;
                        user.AddCredits(credits);
                        await CreditTransactionAsync(user, credits, transactionRef, ct);
                    }
                    break;
            }
        }
        else
        {
            sp = StudentPackage.Create(studentId, package.Id, package.ValidityDays);
            await studentPackageRepository.AddAsync(sp, ct);
            await CreateAllocationsAsync(sp, package, studentId, ct);

            // Credita no User.Credits — FIX CRÍTICO
            if (user is not null)
            {
                var credits = package.CreditsPerMember ?? package.Credits;
                user.AddCredits(credits);
                await CreditTransactionAsync(user, credits, transactionRef, ct);
                await userRepository.SaveAsync(ct);
            }
        }

        await studentPackageRepository.SaveAsync(ct);
    }

    private async Task CreditTransactionAsync(
        User user, int credits, string? transactionRef, CancellationToken ct)
    {
        var tx = CreditTransaction.Create(
            user.Id, credits, user.Credits,
            $"Package activated: {transactionRef ?? "manual"}",
            "package", null);
        await creditTransactionRepository.AddAsync(tx, ct);
        await creditTransactionRepository.SaveAsync(ct);
    }

    private async Task CreateAllocationsAsync(
        StudentPackage sp, Package package, Guid studentId, CancellationToken ct)
    {
        var dependents = (await dependentRepository.ListByStudentAsync(studentId, ct))
            .Where(d => d.Active).ToList();

        var totalPersons = 1 + dependents.Count;
        var creditsPerPerson = package.CreditsPerMember
            ?? package.Credits / totalPersons;

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