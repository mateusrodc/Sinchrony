using FluentAssertions;
using Moq;
using Sinchrony.Application.Payments;
using Sinchrony.Application.Payments.Commands;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// Compra de carrinho com 2 pacotes (aluna Tainara, 27/10): uma cobrança de R$ 98,90 gravava R$ 98,90
// em cada pacote e só creditava, sem criar StudentPackage.
public class CartPurchaseTests
{
    private static Package Pkg(string name, decimal price, int credits, string strategy = "block", bool single = false)
    {
        var p = Package.Create(name, null, credits, price, 30, false, true, 0, purchaseStrategy: strategy);
        p.SetSingleClass(single);
        return p;
    }

    [Fact]
    public void Split_PartsAreEachPackagePriceAndSumToTheCharge()
    {
        var parts = PurchaseAmountSplitter.Split(
            [Pkg("Aula Avulsa", 29.90m, 1, single: true), Pkg("1ª Experiência", 69m, 2)], 98.90m);

        parts.Should().Equal(29.90m, 69.00m);
        parts.Sum().Should().Be(98.90m);
    }

    [Fact]
    public void Split_WithCoupon_DistributesProportionallyAndKeepsTheTotal()
    {
        var parts = PurchaseAmountSplitter.Split([Pkg("A", 30m, 1), Pkg("B", 70m, 2)], 90m);

        parts.Sum().Should().Be(90m);
        parts[0].Should().Be(27m);
    }

    [Fact]
    public void Split_SinglePackage_IsTheWholeCharge()
        => PurchaseAmountSplitter.Split([Pkg("A", 50m, 1)], 45m).Should().Equal(45m);

    private readonly Mock<IStudentPackageRepository> _sps = new();
    private readonly Mock<IDependentPackageAllocationRepository> _allocs = new();
    private readonly Mock<IDependentRepository> _deps = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly User _student = User.Create("Tainara", "t@test.com", null, "h", Role.student);

    private PurchasePackageService Service()
    {
        _sps.Setup(r => r.AddAsync(It.IsAny<StudentPackage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _sps.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _allocs.Setup(r => r.AddAsync(It.IsAny<DependentPackageAllocation>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _users.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _users.Setup(r => r.GetByIdAsync(_student.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_student);
        _deps.Setup(r => r.ListByStudentAsync(_student.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Dependent>());
        return new PurchasePackageService(_sps.Object, _allocs.Object, _deps.Object, _users.Object, new Mock<IAsaasService>().Object);
    }

    [Fact]
    public async Task Grant_WithoutActivePackage_CreatesStudentPackageAndCredits()
    {
        _sps.Setup(r => r.GetActiveByStudentAsync(_student.Id, It.IsAny<CancellationToken>())).ReturnsAsync((StudentPackage?)null);

        var result = await Service().GrantCartPackageAsync(_student.Id, Pkg("1ª Experiência", 69m, 2));

        result.CreditsAdded.Should().Be(2);
        result.FellBack.Should().BeFalse();
        _sps.Verify(r => r.AddAsync(It.IsAny<StudentPackage>(), It.IsAny<CancellationToken>()), Times.Once);
        _student.Credits.Should().Be(2);
    }

    [Fact]
    public async Task Grant_WhenStrategyRefusesAfterPayment_StillCreditsWhatWasPaid()
    {
        var active = StudentPackage.Create(_student.Id, Guid.NewGuid(), 30);
        typeof(StudentPackage).GetProperty(nameof(StudentPackage.Package))!.SetValue(active, Pkg("Plano", 100m, 8));
        _sps.Setup(r => r.GetActiveByStudentAsync(_student.Id, It.IsAny<CancellationToken>())).ReturnsAsync(active);

        var result = await Service().GrantCartPackageAsync(_student.Id, Pkg("Outro", 50m, 3, strategy: "block"));

        result.FellBack.Should().BeTrue();
        result.CreditsAdded.Should().Be(3);
        _student.Credits.Should().Be(3);
    }
}

public class CartPurchaseGuardTests
{
    private static Package Pkg(string name, string strategy = "block", bool single = false)
    {
        var p = Package.Create(name, null, 8, 100m, 30, false, true, 0, purchaseStrategy: strategy);
        p.SetSingleClass(single);
        return p;
    }

    private static void ShouldBeRefused(Action act, string containing)
    {
        var ex = Assert.Throws<Sinchrony.Domain.Exceptions.DomainException>(act);
        ex.Code.Should().Be("ACTIVE_PACKAGE_EXISTS");
        ex.HttpStatus.Should().Be(409);
        ex.Message.Should().Contain(containing);
    }

    [Fact]
    public void BlockPackage_WithActivePackage_IsRefusedWithItsName()
        => ShouldBeRefused(
            () => CartPurchaseGuard.EnsureCanBuy(Pkg("Plano"), true, [Pkg("1ª Experiência")]),
            "1ª Experiência");

    [Fact]
    public void TwoBlockPlansInTheSameCart_SecondIsRefused()
        => ShouldBeRefused(
            () => CartPurchaseGuard.EnsureCanBuy(null, false, [Pkg("Start"), Pkg("1ª Experiência")]),
            "1ª Experiência");

    [Fact]
    public void ActiveAulaAvulsa_DoesNotBlockARealPlan()
        => CartPurchaseGuard.EnsureCanBuy(Pkg("Aula Avulsa", single: true), true, [Pkg("1ª Experiência")]);

    [Fact]
    public void AulaAvulsaPlusPlan_WithoutActive_IsAllowedEvenIfBothAreBlock()
        => CartPurchaseGuard.EnsureCanBuy(
            null, false, [Pkg("Aula Avulsa", single: true), Pkg("1ª Experiência")]);

    [Fact]
    public void QueueStrategy_WithActivePackage_IsAllowed()
        => CartPurchaseGuard.EnsureCanBuy(Pkg("Plano"), true, [Pkg("Start", strategy: "queue")]);
}
