using FluentAssertions;
using Sinchrony.Domain.Entities;
using Xunit;

namespace Sinchrony.Tests.Unit.Entities;

// DEMANDA_CORRECOES_RECORRENCIA_V2_BACKEND.md item 6.4 e a revisão anterior: nenhum teste cobria
// a máquina de estados de renovação automática antes disso — os dois bugs críticos da revisão
// (retentativa ignorando +1d/+3d, overdue cobrando pra sempre) teriam sido pegos aqui.
public class StudentPackageRenewalTests
{
    private static StudentPackage CreateAutoRenewing(int validityDays = 30)
    {
        var package = Package.Create("Essence (Recorrente)", null, credits: 8, price: 199.90m,
            validityDays: validityDays, popular: false, active: true, displayOrder: 0);

        var sp = StudentPackage.Create(Guid.NewGuid(), package.Id, validityDays);
        typeof(StudentPackage).GetProperty(nameof(StudentPackage.Package))!.SetValue(sp, package);
        sp.EnableAutoRenew(Guid.NewGuid());
        return sp;
    }

    [Fact]
    public void RecordRenewalFailure_FirstAttempt_SchedulesRetryOneDayFromProblemSince()
    {
        var sp = CreateAutoRenewing();
        var before = DateTime.UtcNow;

        sp.RecordRenewalFailure("Cartão recusado");

        sp.RenewalAttempts.Should().Be(1);
        sp.PaymentStatus.Should().Be(SubscriptionPaymentStatus.retrying);
        sp.ProblemSince.Should().NotBeNull();
        sp.ProblemSince.Should().BeOnOrAfter(before);
        sp.NextRenewalAttemptAt.Should().BeCloseTo(sp.ProblemSince!.Value.AddDays(1), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordRenewalFailure_SecondAttempt_SchedulesRetryThreeDaysFromSameProblemSince()
    {
        var sp = CreateAutoRenewing();

        sp.RecordRenewalFailure("Cartão recusado");
        var problemSince = sp.ProblemSince;

        sp.RecordRenewalFailure("Cartão recusado de novo");

        sp.RenewalAttempts.Should().Be(2);
        sp.ProblemSince.Should().Be(problemSince); // não reinicia na 2ª falha
        sp.NextRenewalAttemptAt.Should().BeCloseTo(problemSince!.Value.AddDays(3), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordRenewalFailure_ThirdAttempt_StillJustSchedulesRetry_CallerDecidesToBlock()
    {
        // RecordRenewalFailure não bloqueia sozinho — quem decide "esgotou, bloqueia" é
        // RecurringRenewalService.ApplyRenewalFailureAsync, olhando RenewalAttempts >= 3.
        var sp = CreateAutoRenewing();
        sp.RecordRenewalFailure(null);
        sp.RecordRenewalFailure(null);

        sp.RecordRenewalFailure(null);

        sp.RenewalAttempts.Should().Be(3);
        sp.PaymentStatus.Should().Be(SubscriptionPaymentStatus.retrying);
    }

    [Fact]
    public void MarkOverdue_ClearsNextRenewalAttemptAt()
    {
        // Bug crítico da revisão: sem isso, o NextRenewalAttemptAt deixado pela 3ª
        // RecordRenewalFailure (ProblemSince + 3 dias, já no passado assim que essa mesma
        // tentativa esgota) fazia o job cobrar de novo a cada tick pra sempre.
        var sp = CreateAutoRenewing();
        sp.RecordRenewalFailure(null);
        sp.RecordRenewalFailure(null);
        sp.RecordRenewalFailure(null);
        sp.NextRenewalAttemptAt.Should().NotBeNull();

        sp.MarkOverdue();

        sp.PaymentStatus.Should().Be(SubscriptionPaymentStatus.overdue);
        sp.NextRenewalAttemptAt.Should().BeNull();
    }

    [Fact]
    public void SetRenewalCard_WhileRetrying_ResetsAttemptsAndSchedulesImmediateRetry()
    {
        var sp = CreateAutoRenewing();
        sp.RecordRenewalFailure("Cartão recusado");
        sp.RenewalAttempts.Should().Be(1);

        sp.SetRenewalCard(Guid.NewGuid());

        sp.RenewalAttempts.Should().Be(0);
        sp.ProblemSince.Should().BeNull();
        sp.NextRenewalAttemptAt.Should().NotBeNull();
        sp.NextRenewalAttemptAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void SetRenewalCard_WhileOverdue_SchedulesImmediateRetry()
    {
        var sp = CreateAutoRenewing();
        sp.RecordRenewalFailure(null);
        sp.MarkOverdue();
        sp.NextRenewalAttemptAt.Should().BeNull();

        sp.SetRenewalCard(Guid.NewGuid());

        sp.NextRenewalAttemptAt.Should().NotBeNull();
        sp.NextRenewalAttemptAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void SetRenewalCard_WhileUpToDate_DoesNotScheduleRetry()
    {
        var sp = CreateAutoRenewing();

        sp.SetRenewalCard(Guid.NewGuid());

        sp.NextRenewalAttemptAt.Should().BeNull();
    }

    [Fact]
    public void KeepOverdueAfterRetryFailure_StaysOverdueWithoutSchedulingAnotherAttempt()
    {
        var sp = CreateAutoRenewing();
        sp.RecordRenewalFailure(null);
        sp.MarkOverdue();
        var attemptsBefore = sp.RenewalAttempts;

        sp.KeepOverdueAfterRetryFailure("Cartão novo também recusado");

        sp.PaymentStatus.Should().Be(SubscriptionPaymentStatus.overdue);
        sp.NextRenewalAttemptAt.Should().BeNull();
        sp.RenewalAttempts.Should().Be(attemptsBefore); // não conta como nova tentativa do ciclo
    }

    [Fact]
    public void ChargeRenewed_WhenPreviousEndDateIsInFuture_ChainsFromIt()
    {
        var sp = CreateAutoRenewing(validityDays: 90);
        var originalEnd = sp.EndDate; // ~90 dias no futuro

        sp.ChargeRenewed();

        sp.StartDate.Should().Be(originalEnd);
        sp.EndDate.Should().Be(originalEnd.AddDays(90));
        sp.Status.Should().Be(StudentPackageStatus.active);
    }

    [Fact]
    public void ChargeRenewed_WhenPreviousEndDateIsFarInThePast_StartsFromNowInstead()
    {
        // Regularizar semanas depois de overdue: encadear do EndDate antigo geraria um EndDate
        // novo ainda no passado, e o PackageExpirationService zeraria os créditos recém-pagos.
        var sp = CreateAutoRenewing(validityDays: 30);
        sp.ExtendValidity(-60); // EndDate vai pra ~30 dias no passado
        var before = DateTime.UtcNow;

        sp.ChargeRenewed();

        sp.StartDate.Should().BeOnOrAfter(before);
        sp.EndDate.Should().BeAfter(DateTime.UtcNow);
        sp.Status.Should().Be(StudentPackageStatus.active);
    }

    [Fact]
    public void ChargeRenewed_ReactivatesExpiredStatus()
    {
        // Renovação "pending" (antifraude) que confirma depois do pacote já ter expirado pela
        // varredura normal — sem isso o aluno pagava e ficava sem pacote mesmo assim.
        var sp = CreateAutoRenewing();
        sp.Expire();
        sp.Status.Should().Be(StudentPackageStatus.expired);

        sp.ChargeRenewed();

        sp.Status.Should().Be(StudentPackageStatus.active);
    }

    [Fact]
    public void ChargeRenewed_ResetsRetryState()
    {
        var sp = CreateAutoRenewing();
        sp.RecordRenewalFailure("Falhou antes de confirmar");

        sp.ChargeRenewed();

        sp.RenewalAttempts.Should().Be(0);
        sp.NextRenewalAttemptAt.Should().BeNull();
    }

    [Fact]
    public void CancelRenewal_TurnsOffAutoRenewWithoutTouchingStatusOrEndDate()
    {
        var sp = CreateAutoRenewing();
        var status = sp.Status;
        var endDate = sp.EndDate;

        sp.CancelRenewal();

        sp.AutoRenew.Should().BeFalse();
        sp.PaymentStatus.Should().Be(SubscriptionPaymentStatus.cancelled);
        sp.Status.Should().Be(status); // pacote continua válido até o EndDate
        sp.EndDate.Should().Be(endDate);
    }

    [Fact]
    public void Cancel_AlsoTurnsOffAutoRenew()
    {
        var sp = CreateAutoRenewing();

        sp.Cancel();

        sp.Status.Should().Be(StudentPackageStatus.cancelled);
        sp.AutoRenew.Should().BeFalse();
        sp.PaymentStatus.Should().Be(SubscriptionPaymentStatus.cancelled);
    }
}
