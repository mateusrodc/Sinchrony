using FluentAssertions;
using Moq;
using Sinchrony.Application.Payments.Commands;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
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
    private readonly Mock<IAsaasService> _asaasService = new();

    public PurchasePackageServiceTests()
    {
        _studentPackageRepo.Setup(r => r.AddAsync(It.IsAny<StudentPackage>(), default)).Returns(Task.CompletedTask);
        _studentPackageRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
        _allocationRepo.Setup(r => r.AddAsync(It.IsAny<DependentPackageAllocation>(), default)).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
    }

    private PurchasePackageService CreateService() =>
        new(_studentPackageRepo.Object, _allocationRepo.Object, _dependentRepo.Object, _userRepo.Object, _asaasService.Object);

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
    public async Task ProcessAsync_QueueStrategyWithActivePackage_QueuesWithoutCreditingAndReportsZero()
    {
        // Regressão do extrato mostrando "+8, saldo 0": pacote na fila não credita nada agora,
        // e o resultado precisa dizer isso pra quem grava a CreditTransaction.
        var package = Package.Create("Essence (Mensal)", null, credits: 8, price: 300m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "queue", maxDependents: 0);

        var student = CreateStudent();
        var active = StudentPackage.Create(student.Id, Guid.NewGuid(), 30);

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync(active);
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        var result = await CreateService().ProcessAsync(student.Id, package, "manual", default);

        result.CreditsAdded.Should().Be(0);
        result.StudentPackage!.Status.Should().Be(StudentPackageStatus.queued);
        student.Credits.Should().Be(0);
    }

    private static Package NewPackage(string name, int credits, string strategy, bool singleClass = false)
    {
        var p = Package.Create(name, null, credits, price: 100m, validityDays: 30,
            popular: false, active: true, displayOrder: 0,
            purchaseStrategy: strategy, maxDependents: 0);
        p.SetSingleClass(singleClass);
        return p;
    }

    // StudentPackage.Package tem setter privado; nos testes de domínio simulamos o Include do EF.
    private static StudentPackage ActiveWith(Guid studentId, Package package)
    {
        var sp = StudentPackage.Create(studentId, package.Id, 30);
        typeof(StudentPackage).GetProperty(nameof(StudentPackage.Package))!.SetValue(sp, package);
        return sp;
    }

    [Fact]
    public async Task ProcessAsync_SingleClassActive_QueueStrategyPlan_ActivatesImmediatelyAndKeepsLeftoverCredits()
    {
        // Bug: aluna só com Aula Avulsa comprava o Essence (estratégia "queue") e ele ficava
        // "Na fila" por até 30 dias, sem crédito. A avulsa não deve atrapalhar.
        var avulsa = NewPackage("Aula Avulsa", 1, "sum_credits", singleClass: true);
        var essence = NewPackage("Essence (Mensal)", 8, "queue");

        var student = CreateStudent();
        student.AddCredits(1); // crédito que sobrou da avulsa
        var active = ActiveWith(student.Id, avulsa);

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync(active);
        _dependentRepo.Setup(r => r.ListByStudentAsync(student.Id, default)).ReturnsAsync(Array.Empty<Dependent>());
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        var result = await CreateService().ProcessAsync(student.Id, essence, "manual", default);

        active.Status.Should().Be(StudentPackageStatus.cancelled);
        result.StudentPackage!.Status.Should().Be(StudentPackageStatus.active);
        result.StudentPackage.PackageId.Should().Be(essence.Id);
        result.CreditsAdded.Should().Be(8);
        student.Credits.Should().Be(9); // 1 que sobrou + 8 do Essence
    }

    [Fact]
    public async Task ProcessAsync_SingleClassActive_BlockStrategyPlan_AlsoActivatesImmediately()
    {
        var avulsa = NewPackage("Aula Avulsa", 1, "sum_credits", singleClass: true);
        var plano = NewPackage("Plano", 8, "block");

        var student = CreateStudent();
        var active = ActiveWith(student.Id, avulsa);

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync(active);
        _dependentRepo.Setup(r => r.ListByStudentAsync(student.Id, default)).ReturnsAsync(Array.Empty<Dependent>());
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        var result = await CreateService().ProcessAsync(student.Id, plano, "purchase", default);

        result.StudentPackage!.Status.Should().Be(StudentPackageStatus.active);
        student.Credits.Should().Be(8);
    }

    [Fact]
    public async Task ProcessAsync_RealPlanActive_QueueStrategyPlan_StillQueues()
    {
        // O comportamento de fila continua valendo pra plano real comprado com plano real ativo.
        var essence = NewPackage("Essence (Mensal)", 8, "queue");

        var student = CreateStudent();
        var active = ActiveWith(student.Id, NewPackage("Essence (Mensal)", 8, "queue"));

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync(active);
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        var result = await CreateService().ProcessAsync(student.Id, essence, "manual", default);

        active.Status.Should().Be(StudentPackageStatus.active);
        result.StudentPackage!.Status.Should().Be(StudentPackageStatus.queued);
        result.CreditsAdded.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_SingleClassActive_BuyingAnotherSingleClass_FollowsItsOwnStrategy()
    {
        var avulsa = NewPackage("Aula Avulsa", 1, "sum_credits", singleClass: true);

        var student = CreateStudent();
        student.AddCredits(1);
        var active = ActiveWith(student.Id, avulsa);

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync(active);
        _allocationRepo.Setup(r => r.GetByStudentPackageAndDependentAsync(active.Id, null, default))
            .ReturnsAsync((DependentPackageAllocation?)null);
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        var result = await CreateService().ProcessAsync(student.Id, avulsa, "purchase", default);

        active.Status.Should().Be(StudentPackageStatus.active);
        result.CreditsAdded.Should().Be(1);
        student.Credits.Should().Be(2);
    }

    [Fact]
    public async Task ProcessAsync_NoActivePackage_ReportsCreditsAdded()
    {
        var package = Package.Create("Essence (Mensal)", null, credits: 8, price: 300m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "queue", maxDependents: 0);

        var student = CreateStudent();

        _studentPackageRepo.Setup(r => r.GetActiveByStudentAsync(student.Id, default)).ReturnsAsync((StudentPackage?)null);
        _dependentRepo.Setup(r => r.ListByStudentAsync(student.Id, default)).ReturnsAsync(Array.Empty<Dependent>());
        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);

        var result = await CreateService().ProcessAsync(student.Id, package, "manual", default);

        result.CreditsAdded.Should().Be(8);
        result.StudentPackage!.Status.Should().Be(StudentPackageStatus.active);
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
