using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Services;
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

    // DEMANDA_RENOVACAO_CREDITOS_EXPIRAM_BACKEND.md (Termos de Uso 6.3): virada de ciclo de uma
    // renovação automática já paga — créditos não usados do ciclo anterior não acumulam pro
    // seguinte, e as cotas de dependentes são recriadas junto (a renovação antiga não fazia isso).
    private StudentPackage AddAutoRenewingDueForTurnover(Package package, int usedOutOf)
    {
        var sp = StudentPackage.Create(_student.Id, package.Id, validityDays: -1); // EndDate no passado
        sp.SetSource("purchase", package.Credits);
        sp.EnableAutoRenew(null);
        sp.MarkRenewalPaidForCycle(); // renovação já paga antes do EndDate, aguardando a virada
        _db.StudentPackages.Add(sp);

        var remaining = package.Credits - usedOutOf;
        _db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(sp.Id, null, remaining));
        if (remaining > 0)
            _student.AddCredits(remaining);
        return sp;
    }

    [Fact]
    public async Task ListDueForTurnoverAsync_ReturnsOnlyRenewalPaidPackagesPastEndDate()
    {
        var package = AddPackage("Essence (Recorrente)", credits: 8);
        var due = AddAutoRenewingDueForTurnover(package, usedOutOf: 5);

        var notYetDue = StudentPackage.Create(_student.Id, package.Id, validityDays: 30);
        notYetDue.EnableAutoRenew(null);
        notYetDue.MarkRenewalPaidForCycle();
        _db.StudentPackages.Add(notYetDue);

        var pastEndDateButNotPaid = StudentPackage.Create(_student.Id, package.Id, validityDays: -1);
        pastEndDateButNotPaid.EnableAutoRenew(null);
        _db.StudentPackages.Add(pastEndDateButNotPaid);

        await _db.SaveChangesAsync();

        var ids = await _service.ListDueForTurnoverAsync(DateTime.UtcNow);

        ids.Should().ContainSingle().Which.Should().Be(due.Id);
    }

    [Fact]
    public async Task ListExpiredIdsAsync_ExcludesPackagesWithRenewalPaidForCycle()
    {
        var package = AddPackage("Essence (Recorrente)", credits: 8);
        AddAutoRenewingDueForTurnover(package, usedOutOf: 5);
        await _db.SaveChangesAsync();

        var ids = await _service.ListExpiredIdsAsync(DateTime.UtcNow);

        ids.Should().BeEmpty(); // vai pro caminho de virada, não pro de expiração normal
    }

    [Fact]
    public async Task TurnoverRenewalAsync_CreditsDoNotAccumulate_OldBalanceExpiresBeforeNewGrant()
    {
        // Critério de aceite 1: pacote de 8 créditos, aluno usou 5 (sobraram 3) — depois da
        // virada, saldo vira 8 (não 11), com uma transação de expiração (-3) e uma de concessão (+8).
        var package = AddPackage("Essence (Recorrente)", credits: 8);
        var sp = AddAutoRenewingDueForTurnover(package, usedOutOf: 5);
        await _db.SaveChangesAsync();

        var turnedOver = await _service.TurnoverRenewalAsync(sp.Id, DateTime.UtcNow);

        turnedOver.Should().BeTrue();
        _student.Credits.Should().Be(8);

        var txs = await _db.CreditTransactions.OrderBy(t => t.CreatedAt).ToListAsync();
        txs.Should().HaveCount(2);
        txs[0].Amount.Should().Be(-3);
        txs[0].Type.Should().Be("package_expiration");
        txs[1].Amount.Should().Be(8);
        txs[1].Type.Should().Be("renewal_cycle");

        var reloaded = await _db.StudentPackages.Include(x => x.Allocations).SingleAsync(x => x.Id == sp.Id);
        reloaded.Status.Should().Be(StudentPackageStatus.active);
        reloaded.RenewalPaidForCycle.Should().BeFalse();
        reloaded.RenewalAttempts.Should().Be(0);
        reloaded.EndDate.Should().BeAfter(DateTime.UtcNow);
        reloaded.Allocations.Should().ContainSingle().Which.CreditsRemaining.Should().Be(8);
    }

    [Fact]
    public async Task TurnoverRenewalAsync_FamilyPackage_AllocationsResetToFullPerPersonAndDoNotDuplicate()
    {
        // Critério de aceite 2: titular + 1 dependente, 4 créditos cada; dependente usou tudo —
        // depois da virada as duas cotas voltam pra 4 (não fica com uma linha antiga zerada +
        // uma nova, é a mesma StudentPackage vivendo o 2º ciclo).
        var package = Package.Create("Plano Família (Recorrente)", null, credits: 8, price: 300m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "block", maxDependents: 2);
        _db.Packages.Add(package);

        var dependent = Dependent.Create(_student.Id, "Filho");
        _db.Dependents.Add(dependent);

        var sp = StudentPackage.Create(_student.Id, package.Id, validityDays: -1);
        sp.SetSource("purchase", 8);
        sp.EnableAutoRenew(null);
        sp.MarkRenewalPaidForCycle();
        _db.StudentPackages.Add(sp);
        _db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(sp.Id, null, 4)); // titular não usou
        _db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(sp.Id, dependent.Id, 0)); // dependente usou tudo
        _student.AddCredits(4);
        await _db.SaveChangesAsync();

        await _service.TurnoverRenewalAsync(sp.Id, DateTime.UtcNow);

        var allocations = await _db.DependentPackageAllocations
            .Where(a => a.StudentPackageId == sp.Id).ToListAsync();
        allocations.Should().HaveCount(2); // não duplicou — as antigas foram removidas
        allocations.Should().OnlyContain(a => a.CreditsRemaining == 4);
    }

    [Fact]
    public async Task TurnoverAsync_CalledDirectly_ProducesSameResultAsTurnoverRenewalAsync()
    {
        // Critério de aceite 3: RecurringRenewalService chama TurnoverAsync direto quando o
        // pagamento confirma depois do EndDate (retentativa) — não espera o job.
        var package = AddPackage("Essence (Recorrente)", credits: 8);
        var sp = AddAutoRenewingDueForTurnover(package, usedOutOf: 8); // usou tudo
        await _db.SaveChangesAsync();

        var reloaded = await _db.StudentPackages
            .Include(x => x.Package).Include(x => x.Allocations)
            .SingleAsync(x => x.Id == sp.Id);
        var user = await _db.Users.SingleAsync(u => u.Id == _student.Id);

        await _service.TurnoverAsync(reloaded, user, DateTime.UtcNow, default);
        await _db.SaveChangesAsync();

        _student.Credits.Should().Be(8);
        reloaded.Status.Should().Be(StudentPackageStatus.active);
        reloaded.RenewalPaidForCycle.Should().BeFalse();
    }

    [Fact]
    public async Task CancelledRenewal_WithoutRenewalPaidForCycle_StillExpiresNormally()
    {
        // Critério de aceite 4: aluno cancela a renovação (AutoRenew=false,
        // RenewalPaidForCycle continua false) → no EndDate, o pacote expira e os créditos zeram
        // como qualquer pacote não recorrente — sem passar pelo caminho de virada.
        var package = AddPackage("Essence (Recorrente)", credits: 8);
        var sp = StudentPackage.Create(_student.Id, package.Id, validityDays: -1);
        sp.SetSource("purchase", 8);
        sp.EnableAutoRenew(null);
        sp.CancelRenewal(); // aluno desistiu antes de qualquer nova cobrança
        _db.StudentPackages.Add(sp);
        _db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(sp.Id, null, 3));
        _student.AddCredits(3);
        await _db.SaveChangesAsync();

        var dueForTurnover = await _service.ListDueForTurnoverAsync(DateTime.UtcNow);
        dueForTurnover.Should().BeEmpty();

        var expired = await _service.ExpireAsync(sp.Id, DateTime.UtcNow);

        expired.Should().BeTrue();
        _student.Credits.Should().Be(0);
        (await _db.StudentPackages.FindAsync(sp.Id))!.Status.Should().Be(StudentPackageStatus.expired);
    }

    // ── Regra 2 (21/10/2026): o pacote da fila entra quando os créditos do ativo acabam ──────────

    // Pacote ativo dentro da validade, com `balance` créditos no saldo do aluno.
    private StudentPackage AddCurrentActive(Package package, int balance)
    {
        var sp = StudentPackage.Create(_student.Id, package.Id, validityDays: 30);
        sp.SetSource("purchase", package.Credits);
        _db.StudentPackages.Add(sp);
        _db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(sp.Id, null, package.Credits));
        if (balance > 0) _student.AddCredits(balance);
        return sp;
    }

    [Fact]
    public async Task PromoteIfCreditsExhausted_ZeroBalanceWithQueue_ActivatesQueuedNow()
    {
        // Critério de aceite 2: os 2 créditos da 1ª Experiência foram usados; o da fila entra na
        // hora, com os créditos dele e a validade contando a partir de agora (até 23:59:59 BRT).
        var experiencia = AddPackage("1ª Experiência", credits: 2);
        var promo = AddPackage("PROMOCIONAL 8 AULAS", credits: 8, validityDays: 60);
        var active = AddCurrentActive(experiencia, balance: 0);
        var queued = AddQueued(promo);
        await _db.SaveChangesAsync();

        var promoted = await _service.PromoteIfCreditsExhaustedAsync(_student.Id);

        promoted.Should().BeTrue();
        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.expired);
        var reloaded = await _db.StudentPackages.Include(x => x.Allocations).SingleAsync(x => x.Id == queued.Id);
        reloaded.Status.Should().Be(StudentPackageStatus.active);
        reloaded.StartDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        reloaded.EndDate.Should().Be(PackageValidity.End(reloaded.StartDate, 60));
        reloaded.Allocations.Should().ContainSingle().Which.CreditsRemaining.Should().Be(8);
        _student.Credits.Should().Be(8);

        var tx = await _db.CreditTransactions.SingleAsync();
        tx.Amount.Should().Be(8);
        tx.Type.Should().Be("queue_activation");
        tx.Reason.Should().Be("Pacote PROMOCIONAL 8 AULAS ativado da fila (créditos esgotados)");

        var audit = await _db.AuditLogs.SingleAsync();
        audit.Event.Should().Be("package.queue_promoted_credits_exhausted");
        audit.EntityId.Should().Be(queued.Id);
    }

    [Fact]
    public async Task PromoteIfCreditsExhausted_BalanceLeft_DoesNothing()
    {
        var experiencia = AddPackage("1ª Experiência", credits: 2);
        var active = AddCurrentActive(experiencia, balance: 1);
        var queued = AddQueued(AddPackage("PROMOCIONAL 8 AULAS", credits: 8));
        await _db.SaveChangesAsync();

        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeFalse();

        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.active);
        (await _db.StudentPackages.FindAsync(queued.Id))!.Status.Should().Be(StudentPackageStatus.queued);
    }

    [Fact]
    public async Task PromoteIfCreditsExhausted_FamilyDependentStillHasQuota_DoesNotPromote()
    {
        // Critério de aceite 3: titular zerado, mas um dependente ainda com cota.
        var family = Package.Create("Plano Família", null, credits: 8, price: 300m,
            validityDays: 30, popular: false, active: true, displayOrder: 0,
            purchaseStrategy: "queue", maxDependents: 2);
        _db.Packages.Add(family);
        var dependent = Dependent.Create(_student.Id, "Filho");
        _db.Dependents.Add(dependent);
        var active = AddCurrentActive(family, balance: 0);
        _db.DependentPackageAllocations.Add(DependentPackageAllocation.Create(active.Id, dependent.Id, 2));
        AddQueued(AddPackage("PROMOCIONAL 8 AULAS", credits: 8));
        await _db.SaveChangesAsync();

        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeFalse();
        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.active);
    }

    [Fact]
    public async Task PromoteIfCreditsExhausted_PendingPixQueued_IsNotPromoted()
    {
        // Critério de aceite 4.
        var active = AddCurrentActive(AddPackage("1ª Experiência", credits: 2), balance: 0);
        var promo = AddPackage("PROMOCIONAL 8 AULAS", credits: 8);
        var queued = AddQueued(promo, source: "purchase");
        _db.Purchases.Add(Purchase.CreatePending(_student.Id, promo.Id, 100m, "pix", "tx-pend"));
        await _db.SaveChangesAsync();

        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeFalse();

        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.active);
        (await _db.StudentPackages.FindAsync(queued.Id))!.Status.Should().Be(StudentPackageStatus.queued);
    }

    [Fact]
    public async Task PromoteIfCreditsExhausted_ConfirmedPixCreditsInBalance_CountAsExhaustedAndAreNotCreditedTwice()
    {
        // O Pix confirmado do pacote da fila já somou 8 ao saldo: o saldo do ATIVO é 0.
        var active = AddCurrentActive(AddPackage("1ª Experiência", credits: 2), balance: 0);
        var promo = AddPackage("PROMOCIONAL 8 AULAS", credits: 8);
        var queued = AddQueued(promo, source: "purchase");
        _db.Purchases.Add(Purchase.CreateConfirmed(_student.Id, promo.Id, 100m, "pix", "tx-ok"));
        _student.AddCredits(8);
        await _db.SaveChangesAsync();

        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeTrue();

        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.expired);
        (await _db.StudentPackages.FindAsync(queued.Id))!.Status.Should().Be(StudentPackageStatus.active);
        _student.Credits.Should().Be(8);
        (await _db.CreditTransactions.CountAsync()).Should().Be(0);
    }

    // Pacote criado pelo carrinho do App (compra ligada ao StudentPackage) entra na fila com 0 crédito:
    // não é "Pix pré-pago". Antes, o Pix confirmado do mesmo PackageId o fazia passar por pré-pago, o job
    // tirava os créditos do saldo e promovia sem creditar (a aluna perdia o que pagou).
    [Fact]
    public async Task PromoteIfCreditsExhausted_CartQueuedWithConfirmedPix_IsNotTreatedAsPrepaid()
    {
        var active = AddCurrentActive(AddPackage("Essence", credits: 8), balance: 5);
        var start = AddPackage("Start", credits: 8);
        var queued = AddQueued(start, source: "purchase");
        var purchase = Purchase.CreateConfirmed(_student.Id, start.Id, 100m, "pix", "tx-cart");
        purchase.LinkStudentPackage(queued.Id);
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();

        // Ativo ainda tem 5 créditos: nada de promover.
        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeFalse();
        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.active);
        _student.Credits.Should().Be(5);
    }

    [Fact]
    public async Task ExpireAsync_CartQueuedWithConfirmedPix_IsCreditedOnPromotion()
    {
        var expiring = AddExpiredActive(AddPackage("Essence", credits: 8));
        var start = AddPackage("Start", credits: 8);
        var queued = AddQueued(start, source: "purchase");
        var purchase = Purchase.CreateConfirmed(_student.Id, start.Id, 100m, "pix", "tx-cart2");
        purchase.LinkStudentPackage(queued.Id);
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();

        await _service.ExpireAsync(expiring.Id, DateTime.UtcNow);

        (await _db.StudentPackages.FindAsync(queued.Id))!.Status.Should().Be(StudentPackageStatus.active);
        _student.Credits.Should().Be(8);
    }

    // Venda no balcão grava PaymentMethod "pix" mas nunca credita o pacote da fila na venda: não é pré-pago.
    [Fact]
    public async Task PromoteIfCreditsExhausted_CounterPixQueued_IsNotTreatedAsPrepaid()
    {
        var active = AddCurrentActive(AddPackage("Essence", credits: 8), balance: 5);
        var start = AddPackage("Start", credits: 8);
        AddQueued(start, source: "manual");
        _db.Purchases.Add(Purchase.CreateCounterConfirmed(_student.Id, start.Id, 100m, "pix"));
        await _db.SaveChangesAsync();

        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeFalse();
        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.active);
        _student.Credits.Should().Be(5);
    }

    [Fact]
    public async Task PromoteIfCreditsExhausted_RecurringActive_IsNotAffected()
    {
        // Critério de aceite 5: o ciclo do recorrente segue a renovação.
        var essence = AddPackage("Essence (Recorrente)", credits: 8);
        var active = AddCurrentActive(essence, balance: 0);
        active.EnableAutoRenew(null);
        AddQueued(AddPackage("PROMOCIONAL 8 AULAS", credits: 8));
        await _db.SaveChangesAsync();

        (await _service.PromoteIfCreditsExhaustedAsync(_student.Id)).Should().BeFalse();
        (await _service.ListStudentsWithQueueBehindActiveAsync()).Should().BeEmpty();
        (await _db.StudentPackages.FindAsync(active.Id))!.Status.Should().Be(StudentPackageStatus.active);
    }

    [Fact]
    public async Task ListStudentsWithQueueBehindActive_ReturnsOnlyStudentsWithQueue()
    {
        AddCurrentActive(AddPackage("1ª Experiência", credits: 2), balance: 0);
        await _db.SaveChangesAsync();
        (await _service.ListStudentsWithQueueBehindActiveAsync()).Should().BeEmpty();

        AddQueued(AddPackage("PROMOCIONAL 8 AULAS", credits: 8));
        await _db.SaveChangesAsync();
        (await _service.ListStudentsWithQueueBehindActiveAsync()).Should().ContainSingle().Which.Should().Be(_student.Id);
    }
}
