using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// DEMANDA_CORRECOES_RECORRENCIA_V2_BACKEND.md item 1 (CRÍTICO): ListDueForRenewalAsync
// selecionava um pacote "retrying" de novo a cada tick, ignorando o NextRenewalAttemptAt
// agendado pra +1/+3 dias, e nunca parava de selecionar um pacote "overdue". Testes diretos
// contra a query real (não só a lógica de domínio) — é exatamente o que as duas revisões
// pediram ("um teste unitário disso vale mais que qualquer outro").
public class StudentPackageRepositoryRenewalTests
{
    private readonly ApplicationDbContext _db;
    private readonly StudentPackageRepository _repository;
    private readonly Package _package;
    private readonly User _student;

    public StudentPackageRepositoryRenewalTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ApplicationDbContext(options);
        _repository = new StudentPackageRepository(_db);

        _student = User.Create("Student", "student@test.com", null, "hash", Role.student);
        _db.Users.Add(_student);

        _package = Package.Create("Essence (Recorrente)", null, credits: 8, price: 199.90m,
            validityDays: 30, popular: false, active: true, displayOrder: 0);
        _db.Packages.Add(_package);
    }

    private StudentPackage AddAutoRenewing(Action<StudentPackage>? configure = null)
    {
        var sp = StudentPackage.Create(_student.Id, _package.Id, validityDays: 30);
        sp.EnableAutoRenew(null);
        configure?.Invoke(sp);
        _db.StudentPackages.Add(sp);
        _db.SaveChanges();
        return sp;
    }

    private async Task<bool> IsDueAsync(StudentPackage sp) =>
        (await _repository.ListDueForRenewalAsync(DateTime.UtcNow)).Contains(sp.Id);

    [Fact]
    public async Task DueWithin24hAndNoRetrySchedule_IsSelected()
    {
        var sp = AddAutoRenewing(x => x.ExtendValidity(-29)); // EndDate em ~1 dia

        (await IsDueAsync(sp)).Should().BeTrue();
    }

    [Fact]
    public async Task MoreThan24hFromEndDate_IsNotSelected()
    {
        var sp = AddAutoRenewing(); // EndDate em ~30 dias, sem retentativa agendada

        (await IsDueAsync(sp)).Should().BeFalse();
    }

    [Fact]
    public async Task Retrying_WithFutureNextRenewalAttempt_IsNotSelected_EvenWithinEndDateWindow()
    {
        // Bug crítico: antes, o EndDate sozinho bastava — o pacote era recobrado no tick
        // seguinte, ignorando a retentativa agendada pra +1 dia.
        var sp = AddAutoRenewing(x =>
        {
            x.ExtendValidity(-29); // EndDate em ~1 dia — dentro da janela de 24h
            x.RecordRenewalFailure("Cartão recusado"); // agenda NextRenewalAttemptAt = +1 dia
        });

        (await IsDueAsync(sp)).Should().BeFalse();
    }

    [Fact]
    public async Task Retrying_WithPastNextRenewalAttempt_IsSelected()
    {
        var sp = AddAutoRenewing(x =>
        {
            x.RecordRenewalFailure("Cartão recusado");
            x.SetRenewalCard(Guid.NewGuid()); // reagenda pra "agora" (troca de cartão)
        });

        (await IsDueAsync(sp)).Should().BeTrue();
    }

    [Fact]
    public async Task Overdue_WithoutExplicitRetrySchedule_IsNeverSelected()
    {
        // Bug crítico: antes, MarkOverdue não zerava NextRenewalAttemptAt, então o valor deixado
        // pela última falha (já no passado) fazia o job cobrar de novo pra sempre.
        var sp = AddAutoRenewing(x =>
        {
            x.RecordRenewalFailure(null);
            x.RecordRenewalFailure(null);
            x.RecordRenewalFailure(null);
            x.MarkOverdue();
        });

        (await IsDueAsync(sp)).Should().BeFalse();
    }

    [Fact]
    public async Task Overdue_AfterCardUpdate_IsSelectedOnce()
    {
        var sp = AddAutoRenewing(x =>
        {
            x.RecordRenewalFailure(null);
            x.MarkOverdue();
            x.SetRenewalCard(Guid.NewGuid()); // aluno trocou o cartão
        });

        (await IsDueAsync(sp)).Should().BeTrue();
    }

    [Fact]
    public async Task NotAutoRenewing_IsNeverSelected()
    {
        var sp = StudentPackage.Create(_student.Id, _package.Id, validityDays: 30);
        sp.ExtendValidity(-29);
        _db.StudentPackages.Add(sp);
        await _db.SaveChangesAsync();

        (await IsDueAsync(sp)).Should().BeFalse();
    }

    [Fact]
    public async Task NotActive_IsNeverSelected()
    {
        var sp = StudentPackage.CreateQueued(_student.Id, _package.Id, validityDays: 30);
        sp.EnableAutoRenew(null);
        _db.StudentPackages.Add(sp);
        await _db.SaveChangesAsync();

        (await IsDueAsync(sp)).Should().BeFalse();
    }
}
