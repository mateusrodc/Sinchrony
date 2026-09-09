using FluentAssertions;
using Moq;
using Sinchrony.Application.Payments.Commands;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// Regressão do bug: pacote individual (sem dependentes) com CreditsPerMember "fantasma"
// preenchido no banco creditava CreditsPerMember em vez de Credits (ex.: pacote de 1 crédito
// concedia 32 créditos). Ver Package.GetCreditsToGrant() e PurchasePackageService.
public class PurchasePackageServiceTests
{
    private readonly Mock<IStudentPackageRepository> _studentPackageRepo = new();
    private readonly Mock<IDependentPackageAllocationRepository> _allocationRepo = new();
    private readonly Mock<IDependentRepository> _dependentRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();

    public PurchasePackageServiceTests()
    {
        _studentPackageRepo.Setup(r => r.AddAsync(It.IsAny<StudentPackage>(), default)).Returns(Task.CompletedTask);
        _studentPackageRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
        _allocationRepo.Setup(r => r.AddAsync(It.IsAny<DependentPackageAllocation>(), default)).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
    }

    private PurchasePackageService CreateService() =>
        new(_studentPackageRepo.Object, _allocationRepo.Object, _dependentRepo.Object, _userRepo.Object);

    private static User CreateStudent() => User.Create("Student", "student@test.com", null, "hash", Role.student);

    [Fact]
    public async Task ProcessAsync_IndividualPackageWithStrayCreditsPerMember_GrantsOnlyPackageCredits()
    {
        // Pacote "Aula Avulsa": 1 crédito, sem dependentes, mas com CreditsPerMember=32
        // deixado no banco de uma configuração anterior (o dado "fantasma" do incidente real).
        var package = Package.Create("Aula Avulsa", null, credits: 1, price: 89m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "block", maxDependents: 0);
        package.UpdateFull(
            package.Name, package.Description, package.Credits, package.Price,
            package.ValidityDays, package.Popular, package.Active, package.DisplayOrder,
            null, package.PurchaseStrategy, maxDependents: 0, creditsPerMember: 32,
            null, null, null, null, null, null, null, null, null, null, null,
            true, null, 30);

        var student = CreateStudent();

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync((StudentPackage?)null);
        _dependentRepo.Setup(r => r.ListByStudentAsync(student.Id, default)).ReturnsAsync(Array.Empty<Dependent>());
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        await CreateService().ProcessAsync(student.Id, package, "manual", default);

        student.Credits.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_IndividualPackage_AllocationAlsoUsesPackageCredits_NotCreditsPerMember()
    {
        var package = Package.Create("Aula Avulsa", null, credits: 1, price: 89m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "block", maxDependents: 0);
        package.UpdateFull(
            package.Name, package.Description, package.Credits, package.Price,
            package.ValidityDays, package.Popular, package.Active, package.DisplayOrder,
            null, package.PurchaseStrategy, maxDependents: 0, creditsPerMember: 32,
            null, null, null, null, null, null, null, null, null, null, null,
            true, null, 30);

        var student = CreateStudent();
        DependentPackageAllocation? createdAllocation = null;

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync((StudentPackage?)null);
        _dependentRepo.Setup(r => r.ListByStudentAsync(student.Id, default)).ReturnsAsync(Array.Empty<Dependent>());
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);
        _allocationRepo.Setup(r => r.AddAsync(It.IsAny<DependentPackageAllocation>(), default))
            .Callback<DependentPackageAllocation, CancellationToken>((a, _) => createdAllocation = a)
            .Returns(Task.CompletedTask);

        await CreateService().ProcessAsync(student.Id, package, "manual", default);

        createdAllocation.Should().NotBeNull();
        createdAllocation!.CreditsRemaining.Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_FamilyPackage_StillUsesCreditsPerMember()
    {
        // Regressão inversa: pacote de família continua usando CreditsPerMember normalmente.
        var package = Package.Create("Plano Família", null, credits: 20, price: 300m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "block", maxDependents: 2);
        package.UpdateFull(
            package.Name, package.Description, package.Credits, package.Price,
            package.ValidityDays, package.Popular, package.Active, package.DisplayOrder,
            null, package.PurchaseStrategy, maxDependents: 2, creditsPerMember: 5,
            null, null, null, null, null, null, null, null, null, null, null,
            true, null, 30);

        var student = CreateStudent();

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync((StudentPackage?)null);
        _dependentRepo.Setup(r => r.ListByStudentAsync(student.Id, default)).ReturnsAsync(Array.Empty<Dependent>());
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        await CreateService().ProcessAsync(student.Id, package, "manual", default);

        student.Credits.Should().Be(5);
    }
}
