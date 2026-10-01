using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sinchrony.Application.Payments;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Payments;

// DEMANDA_BACKEND_DEMANDAS_PENDENTES item 3: Primeira Experiência — compra única por aluno e por
// família, sem pagamento duplicado. Repositórios reais (EF InMemory) + Asaas/UnitOfWork mockados.
public class OncePerStudentGuardTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAsaasService> _asaas = new();
    private readonly Mock<IPaymentConfirmationService> _confirmation = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly OncePerStudentGuard _guard;

    private readonly User _owner = User.Create("Titular", "t@test.com", null, "h", Role.student);
    private readonly User _dependent = User.Create("Dependente", "d@test.com", null, "h", Role.student);
    private readonly User _stranger = User.Create("Outro", "o@test.com", null, "h", Role.student);
    private readonly Package _firstExperience;
    private readonly Package _regular;

    public OncePerStudentGuardTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);

        _firstExperience = Package.Create("Primeira Experiência", null, 1, 30m, 30, false, true, 0);
        _firstExperience.SetOncePerStudent(true);
        _regular = Package.Create("Pacote 8", null, 8, 200m, 30, false, true, 1);

        _db.Users.AddRange(_owner, _dependent, _stranger);
        _db.Packages.AddRange(_firstExperience, _regular);
        _db.SaveChanges();

        _guard = new OncePerStudentGuard(
            new UserRepository(_db), new PurchaseRepository(_db),
            _asaas.Object, _confirmation.Object, _uow.Object);
    }

    private Purchase Add(User user, Package pkg, string method, string status, string tx = "pay_1",
        DateTime? createdAt = null)
    {
        var p = Purchase.CreatePending(user.Id, pkg.Id, pkg.Price, method, tx);
        if (status == "confirmed") p.Confirm();
        if (status == "failed") p.Fail();
        if (createdAt.HasValue)
            typeof(Purchase).GetProperty(nameof(Purchase.CreatedAt))!.SetValue(p, createdAt.Value);
        _db.Purchases.Add(p);
        _db.SaveChanges();
        return p;
    }

    private Task<Purchase?> Check(User user, PurchaseOrigin origin, params Package[] cart)
        => _guard.CheckAsync(user.Id, cart, origin, CancellationToken.None);

    private static async Task<DomainException> Thrown(Func<Task> act)
        => (await act.Should().ThrowAsync<DomainException>()).Which;

    // ---- compra paga bloqueia ----

    [Fact]
    public async Task OwnConfirmedPurchase_Blocks()
    {
        Add(_owner, _firstExperience, "card", "confirmed");

        var ex = await Thrown(() => Check(_owner, PurchaseOrigin.AppPix, _firstExperience));

        ex.Code.Should().Be("PACKAGE_ALREADY_PURCHASED");
        ex.Message.Should().Be("Este pacote pode ser adquirido apenas uma vez por aluno.");
    }

    [Fact]
    public async Task ConfirmedPurchase_ByDependentUserModel_BlocksResponsible_AndDependent()
    {
        _dependent.SetAsDependent(_owner.Id); // modelo users.IsDependent + ResponsibleStudentId
        _db.SaveChanges();
        Add(_dependent, _firstExperience, "pix", "confirmed");

        (await Thrown(() => Check(_owner, PurchaseOrigin.AppCard, _firstExperience)))
            .Code.Should().Be("PACKAGE_ALREADY_PURCHASED");
        (await Thrown(() => Check(_dependent, PurchaseOrigin.AppCard, _firstExperience)))
            .Code.Should().Be("PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT");
    }

    [Fact]
    public async Task ConfirmedPurchase_ByResponsible_BlocksDependent()
    {
        _dependent.SetAsDependent(_owner.Id);
        _db.SaveChanges();
        Add(_owner, _firstExperience, "cash", "confirmed");

        (await Thrown(() => Check(_dependent, PurchaseOrigin.AppPix, _firstExperience)))
            .Code.Should().Be("PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT");
    }

    [Fact]
    public async Task DependentsTableModel_IsAlsoConsidered()
    {
        // Segundo modelo: tabela dependents (ResponsibleStudentId) com UserId do dependente,
        // sem users.IsDependent preenchido.
        var row = Dependent.Create(_owner.Id, "Dependente");
        row.LinkUser(_dependent.Id);
        _db.Dependents.Add(row);
        _db.SaveChanges();
        Add(_dependent, _firstExperience, "pix", "confirmed");

        (await Thrown(() => Check(_owner, PurchaseOrigin.Counter, _firstExperience)))
            .Code.Should().Be("PACKAGE_ALREADY_PURCHASED");
    }

    [Fact]
    public async Task UnrelatedStudent_IsNotBlocked()
    {
        Add(_owner, _firstExperience, "card", "confirmed");

        (await Check(_stranger, PurchaseOrigin.AppPix, _firstExperience)).Should().BeNull();
    }

    [Fact]
    public async Task FailedPurchase_DoesNotCount()
    {
        Add(_owner, _firstExperience, "card", "failed");

        (await Check(_owner, PurchaseOrigin.AppCard, _firstExperience)).Should().BeNull();
    }

    [Fact]
    public async Task RegularPackage_IsNeverRestricted()
    {
        Add(_owner, _regular, "card", "confirmed");

        (await Check(_owner, PurchaseOrigin.AppCard, _regular)).Should().BeNull();
    }

    [Fact]
    public async Task CounterGrant_IsBlockedAfterAppPurchase_NoAdminException()
    {
        Add(_owner, _firstExperience, "pix", "confirmed");

        (await Thrown(() => Check(_owner, PurchaseOrigin.Counter, _firstExperience)))
            .Code.Should().Be("PACKAGE_ALREADY_PURCHASED");
    }

    [Fact]
    public async Task CounterPurchase_BlocksAppPurchase()
    {
        _db.Purchases.Add(Purchase.CreateCounterConfirmed(_owner.Id, _firstExperience.Id, 0, "courtesy"));
        _db.SaveChanges();

        (await Thrown(() => Check(_owner, PurchaseOrigin.AppPix, _firstExperience)))
            .Code.Should().Be("PACKAGE_ALREADY_PURCHASED");
    }

    // ---- dependente não compra pacote de compra única ----

    [Fact]
    public async Task Dependent_WithoutAnyPurchase_CannotBuy_UserModel()
    {
        _dependent.SetAsDependent(_owner.Id);
        _db.SaveChanges();

        var ex = await Thrown(() => Check(_dependent, PurchaseOrigin.AppPix, _firstExperience));

        ex.Code.Should().Be("PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT");
        ex.HttpStatus.Should().Be(409);
        ex.Message.Should().Be("Este pacote não está disponível para dependentes.");
    }

    [Fact]
    public async Task Dependent_DependentsTableModel_CannotBuy_EvenViaCounter()
    {
        var row = Dependent.Create(_owner.Id, "Dependente");
        row.LinkUser(_dependent.Id);
        _db.Dependents.Add(row);
        _db.SaveChanges();

        (await Thrown(() => Check(_dependent, PurchaseOrigin.Counter, _firstExperience)))
            .Code.Should().Be("PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT");
    }

    [Fact]
    public async Task TwoSiblingDependents_NeitherCanBuy()
    {
        var sibling = User.Create("Irmão", "i.com", null, "h", Role.student);
        _db.Users.Add(sibling);
        _dependent.SetAsDependent(_owner.Id);
        sibling.SetAsDependent(_owner.Id);
        _db.SaveChanges();

        (await Thrown(() => Check(_dependent, PurchaseOrigin.AppCard, _firstExperience)))
            .Code.Should().Be("PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT");
        (await Thrown(() => Check(sibling, PurchaseOrigin.AppCard, _firstExperience)))
            .Code.Should().Be("PACKAGE_NOT_AVAILABLE_FOR_DEPENDENT");
    }

    [Fact]
    public async Task Dependent_CanStillBuyRegularPackage()
    {
        _dependent.SetAsDependent(_owner.Id);
        _db.SaveChanges();

        (await Check(_dependent, PurchaseOrigin.AppPix, _regular)).Should().BeNull();
    }

    [Fact]
    public async Task Owner_WithoutPriorPurchase_BuysNormally_EvenWithDependents()
    {
        _dependent.SetAsDependent(_owner.Id);
        _db.SaveChanges();

        (await Check(_owner, PurchaseOrigin.AppPix, _firstExperience)).Should().BeNull();
    }

    // ---- cobrança em aberto ----

    [Fact]
    public async Task PendingValidPix_SameUser_AppPix_ReturnsSamePixInsteadOfCreatingAnother()
    {
        var pending = Add(_owner, _firstExperience, "pix", "pending", "pay_abc", DateTime.UtcNow.AddHours(-2));

        var reusable = await Check(_owner, PurchaseOrigin.AppPix, _firstExperience);

        reusable.Should().NotBeNull();
        reusable!.TransactionId.Should().Be("pay_abc");
        reusable.Id.Should().Be(pending.Id);
        _asaas.Verify(a => a.CancelPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PendingValidPix_AppCard_IsRefused()
    {
        Add(_owner, _firstExperience, "pix", "pending", "pay_abc", DateTime.UtcNow.AddHours(-2));

        (await Thrown(() => Check(_owner, PurchaseOrigin.AppCard, _firstExperience)))
            .Code.Should().Be("PAYMENT_ALREADY_IN_PROGRESS");
    }

    [Fact]
    public async Task PendingValidPix_CounterGrant_IsRefusedWithAppMessage()
    {
        Add(_owner, _firstExperience, "pix", "pending", "pay_abc", DateTime.UtcNow.AddHours(-2));

        var ex = await Thrown(() => Check(_owner, PurchaseOrigin.Counter, _firstExperience));

        ex.Code.Should().Be("PAYMENT_ALREADY_IN_PROGRESS");
        ex.Message.Should().Be("Existe um pagamento pendente deste pacote pelo app.");
    }

    [Fact]
    public async Task PendingValidPix_OfAnotherFamilyMember_IsRefused_NotShared()
    {
        _dependent.SetAsDependent(_owner.Id);
        _db.SaveChanges();
        Add(_dependent, _firstExperience, "pix", "pending", "pay_dep", DateTime.UtcNow.AddHours(-2));

        // Devolver o PIX do dependente pro titular creditaria o dependente: recusa.
        (await Thrown(() => Check(_owner, PurchaseOrigin.AppPix, _firstExperience)))
            .Code.Should().Be("PAYMENT_ALREADY_IN_PROGRESS");
    }

    [Fact]
    public async Task PendingCard_UnderAntifraud_IsRefused()
    {
        Add(_owner, _firstExperience, "card", "pending", "pay_card");

        var ex = await Thrown(() => Check(_owner, PurchaseOrigin.AppCard, _firstExperience));

        ex.Code.Should().Be("PAYMENT_ALREADY_IN_PROGRESS");
        ex.Message.Should().Be("Já existe um pagamento deste pacote em análise. Aguarde a confirmação.");
    }

    [Fact]
    public async Task Cart_WithReusablePix_AndAnotherPackage_IsRefusedWhole()
    {
        Add(_owner, _firstExperience, "pix", "pending", "pay_abc", DateTime.UtcNow.AddHours(-2));

        (await Thrown(() => Check(_owner, PurchaseOrigin.AppPix, _firstExperience, _regular)))
            .Code.Should().Be("PAYMENT_ALREADY_IN_PROGRESS");
    }

    // ---- PIX vencido ----

    [Fact]
    public async Task ExpiredPix_NotPaid_IsCancelledAtAsaas_MarkedFailed_AndNewChargeAllowed()
    {
        var old = Add(_owner, _firstExperience, "pix", "pending", "pay_old", DateTime.UtcNow.AddDays(-3));
        _asaas.Setup(a => a.GetPaymentAsync("pay_old", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult("OVERDUE", null));

        var reusable = await Check(_owner, PurchaseOrigin.AppPix, _firstExperience);

        reusable.Should().BeNull();
        _asaas.Verify(a => a.CancelPaymentAsync("pay_old", It.IsAny<CancellationToken>()), Times.Once);
        (await _db.Purchases.FindAsync(old.Id))!.Status.Should().Be("failed");
    }

    [Fact]
    public async Task ExpiredPix_ButPaid_ConfirmsThroughNormalFlow_AndRefusesNewPurchase()
    {
        Add(_owner, _firstExperience, "pix", "pending", "pay_paid", DateTime.UtcNow.AddDays(-3));
        _asaas.Setup(a => a.GetPaymentAsync("pay_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusResult("RECEIVED", null));

        (await Thrown(() => Check(_owner, PurchaseOrigin.AppPix, _firstExperience)))
            .Code.Should().Be("PACKAGE_ALREADY_PURCHASED");

        _confirmation.Verify(
            c => c.ConfirmPendingAsync("pay_paid", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _asaas.Verify(a => a.CancelPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PixIsOnlyExpiredAfterTheDueDayEnds()
    {
        var created = new DateTime(2026, 9, 10, 15, 0, 0, DateTimeKind.Utc);
        var p = Purchase.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 10m, "pix", "x");
        typeof(Purchase).GetProperty(nameof(Purchase.CreatedAt))!.SetValue(p, created);

        // dueDate = 11/09: ainda pagável durante todo o dia 11 (mais a folga do fuso)
        OncePerStudentGuard.IsPixExpired(p, new DateTime(2026, 9, 11, 23, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
        OncePerStudentGuard.IsPixExpired(p, new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
    }

    // ---- trava / transação ----

    [Fact]
    public async Task RunLocked_UnrestrictedCart_DoesNotOpenTransaction()
    {
        var result = await _guard.RunLockedAsync(_owner.Id, [_regular], () => Task.FromResult(42), CancellationToken.None);

        result.Should().Be(42);
        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunLocked_Restricted_LocksPerFamilyAndPackage_AndCommits()
    {
        _dependent.SetAsDependent(_owner.Id);
        _db.SaveChanges();

        await _guard.RunLockedAsync(_dependent.Id, [_firstExperience], () => Task.FromResult(1), CancellationToken.None);

        // Titular e dependente disputam a MESMA chave (raiz da família + pacote)
        _uow.Verify(u => u.AcquireAdvisoryLockAsync(
            $"once-per-student:{_owner.Id}:{_firstExperience.Id}", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunLocked_GuardBlock_StillCommitsWhatTheCheckWrote()
    {
        var act = () => _guard.RunLockedAsync<int>(_owner.Id, [_firstExperience],
            () => throw DomainException.Conflict(OncePerStudentGuard.AlreadyPurchasedCode, "x"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunLocked_OtherFailure_RollsBack()
    {
        var act = () => _guard.RunLockedAsync<int>(_owner.Id, [_firstExperience],
            () => throw DomainException.Validation("ASAAS_PIX_ERROR", "x"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _uow.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
