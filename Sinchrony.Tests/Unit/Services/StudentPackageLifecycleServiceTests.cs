using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// Regra (21/09/2026): quando o pacote vence, os créditos restantes expiram junto, e o pacote
// mais antigo da fila é promovido. Antes disso nada expirava StudentPackage.
public class StudentPackageLifecycleServiceTests
{
    private readonly ApplicationDbContext _db;
    private readonly StudentPackageLifecycleService _service;
    private readonly User _student;

    public StudentPackageLifecycleServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _service = new StudentPackageLifecycleService(_db);

        _student = User.Create("Student", "student@test.com", null, "hash", Role.student);
        _db.Users.Add(_student);
    }

    private Package AddPackage(string name, int credits, int validityDays = 30)
    {
        var package = Package.Create(name, null, credits, 100m, validityDays,
            popular: false, active: true, displayOrder: 0, purchaseStrategy: "queue");
        _db.Packages.Add(package);
        return package;
    }

    // Pacote ativo com a vigência já vencida (validityDays negativo → EndDate no passado).
    private StudentPackage AddExpiredActive(Package package)
    {
        var sp = StudentPackage.Create(_student.Id, package.Id, validityDays: -1);
        sp.SetSource("purchase", package.Credits);
        _db.StudentPackages.Add(sp);
        _db.DependentPackageAllocations.Add(
            DependentPackageAllocation.Create(sp.Id, null, package.Credits));
        return sp;
    }

    private StudentPackage AddQueued(Package package, string source = "manual")
    {
        var sp = StudentPackage.CreateQueued(_student.Id, package.Id, package.ValidityDays);
        sp.SetSource(source, 0);
        _db.StudentPackages.Add(sp);
        return sp;
    }

    [Fact]
    public async Task ExpireAsync_ExpiredPackage_ZeroesRemainingCreditsAndRecordsTransaction()
    {
        var package = AddPackage("Aula Avulsa", credits: 8);
        var sp = AddExpiredActive(package);
        _student.AddCredits(5); // sobraram 5 dos 8
        await _db.SaveChangesAsync();

        var expired = await _service.ExpireAsync(sp.Id, DateTime.UtcNow);

        expired.Should().BeTrue();
        var reloaded = await _db.StudentPackages.Include(x => x.Allocations).SingleAsync(x => x.Id == sp.Id);
        reloaded.Status.Should().Be(StudentPackageStatus.expired);
        reloaded.Allocations.Should().OnlyContain(a => a.CreditsRemaining == 0);
        _student.Credits.Should().Be(0);

        var tx = await _db.CreditTransactions.SingleAsync();
        tx.Amount.Should().Be(-5);
        tx.BalanceAfter.Should().Be(0);
        tx.Type.Should().Be("package_expiration");
    }

    [Fact]
    public async Task ExpireAsync_NoRemainingCredits_DoesNotRecordTransaction()
    {
        var package = AddPackage("Aula Avulsa", credits: 8);
        var sp = AddExpiredActive(package);
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(sp.Id, DateTime.UtcNow);

        (await _db.CreditTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpireAsync_PackageNotYetExpired_IsLeftUntouched()
    {
        var package = AddPackage("Essence", credits: 8);
        var sp = StudentPackage.Create(_student.Id, package.Id, validityDays: 30);
        _db.StudentPackages.Add(sp);
        _student.AddCredits(8);
        await _db.SaveChangesAsync();

        var expired = await _service.ExpireAsync(sp.Id, DateTime.UtcNow);

        expired.Should().BeFalse();
        _student.Credits.Should().Be(8);
        (await _db.StudentPackages.SingleAsync()).Status.Should().Be(StudentPackageStatus.active);
    }

    [Fact]
    public async Task ExpireAsync_QueuedPackage_IsActivatedAndCredited()
    {
        var avulsa = AddPackage("Aula Avulsa", credits: 1);
        var essence = AddPackage("Essence (Mensal)", credits: 8);
        var expiring = AddExpiredActive(avulsa);
        var queued = AddQueued(essence);
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(expiring.Id, DateTime.UtcNow);

        var promoted = await _db.StudentPackages.Include(x => x.Allocations).SingleAsync(x => x.Id == queued.Id);
        promoted.Status.Should().Be(StudentPackageStatus.active);
        promoted.EndDate.Should().BeAfter(DateTime.UtcNow.AddDays(29));
        promoted.CreditsGranted.Should().Be(8);
        promoted.Allocations.Should().ContainSingle().Which.CreditsRemaining.Should().Be(8);
        _student.Credits.Should().Be(8);

        var tx = await _db.CreditTransactions.SingleAsync();
        tx.Amount.Should().Be(8);
        tx.BalanceAfter.Should().Be(8);
        tx.Type.Should().Be("queue_activation");
    }

    [Fact]
    public async Task ExpireAsync_LeftoverCreditsExpireBeforeQueuedPackageIsCredited()
    {
        var avulsa = AddPackage("Aula Avulsa", credits: 4);
        var essence = AddPackage("Essence (Mensal)", credits: 8);
        var expiring = AddExpiredActive(avulsa);
        AddQueued(essence);
        _student.AddCredits(3);
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(expiring.Id, DateTime.UtcNow);

        _student.Credits.Should().Be(8); // 3 antigos expiraram, 8 novos entraram — não acumulam
    }

    [Fact]
    public async Task ExpireAsync_OnlyOldestQueuedPackageIsPromoted()
    {
        var avulsa = AddPackage("Aula Avulsa", credits: 1);
        var essence = AddPackage("Essence (Mensal)", credits: 8);
        var expiring = AddExpiredActive(avulsa);
        var first = AddQueued(essence);
        var second = AddQueued(essence);
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(expiring.Id, DateTime.UtcNow);

        (await _db.StudentPackages.FindAsync(first.Id))!.Status.Should().Be(StudentPackageStatus.active);
        (await _db.StudentPackages.FindAsync(second.Id))!.Status.Should().Be(StudentPackageStatus.queued);
    }

    [Fact]
    public async Task ExpireAsync_ConfirmedPixQueuedPackage_KeepsItsCreditsAndDoesNotCreditAgain()
    {
        // O webhook do Asaas já creditou os 8 quando o PIX foi confirmado, mesmo com o pacote
        // ainda na fila. A expiração do pacote antigo não pode apagar esses créditos, e a
        // promoção não pode creditar de novo.
        var avulsa = AddPackage("Aula Avulsa", credits: 1);
        var essence = AddPackage("Essence (Mensal)", credits: 8);
        var expiring = AddExpiredActive(avulsa);
        var queued = AddQueued(essence, source: "purchase");
        _db.Purchases.Add(Purchase.CreateConfirmed(_student.Id, essence.Id, 100m, "pix", "tx-1"));
        _student.AddCredits(1 + 8); // 1 do pacote antigo + 8 do PIX
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(expiring.Id, DateTime.UtcNow);

        _student.Credits.Should().Be(8);
        (await _db.StudentPackages.FindAsync(queued.Id))!.Status.Should().Be(StudentPackageStatus.active);
        (await _db.CreditTransactions.Where(t => t.Type == "queue_activation").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExpireAsync_PendingPixQueuedPackage_IsNotPromoted()
    {
        var avulsa = AddPackage("Aula Avulsa", credits: 1);
        var essence = AddPackage("Essence (Mensal)", credits: 8);
        var expiring = AddExpiredActive(avulsa);
        var queued = AddQueued(essence, source: "purchase");
        _db.Purchases.Add(Purchase.CreatePending(_student.Id, essence.Id, 100m, "pix", "tx-2"));
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(expiring.Id, DateTime.UtcNow);

        (await _db.StudentPackages.FindAsync(queued.Id))!.Status.Should().Be(StudentPackageStatus.queued);
        _student.Credits.Should().Be(0);
    }

    [Fact]
    public async Task ListExpiredIdsAsync_ReturnsOnlyActivePackagesPastEndDate()
    {
        var package = AddPackage("Aula Avulsa", credits: 1);
        var expired = AddExpiredActive(package);
        var current = StudentPackage.Create(_student.Id, package.Id, validityDays: 30);
        _db.StudentPackages.Add(current);
        await _db.SaveChangesAsync();

        var ids = await _service.ListExpiredIdsAsync(DateTime.UtcNow);

        ids.Should().ContainSingle().Which.Should().Be(expired.Id);
    }
}
