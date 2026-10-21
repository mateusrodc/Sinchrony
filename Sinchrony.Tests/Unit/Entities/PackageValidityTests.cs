using FluentAssertions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Entities;

// DEMANDA_VALIDADE_FIM_DO_DIA_E_FILA_AO_ZERAR_CREDITOS_BACKEND.md, Regra 1 (21/10/2026): a
// validade vai até 23:59:59 (Brasília) do último dia, não mais até o horário da compra.
public class PackageValidityTests
{
    private static DateTime Utc(int y, int m, int d, int h, int min, int s = 0)
        => new(y, m, d, h, min, s, DateTimeKind.Utc);

    [Fact]
    public void End_PurchaseAt1625Brt_ValidUntilEndOfLastDayBrt()
    {
        // Critério de aceite 1: comprado em 21/09 às 16:25 BRT (19:25 UTC), 30 dias.
        var end = PackageValidity.End(Utc(2026, 9, 21, 19, 25), 30);

        end.Should().Be(Utc(2026, 10, 22, 2, 59, 59)); // 21/10 23:59:59 BRT
        // Às 23:00 BRT do dia 21/10 (02:00 UTC do dia 22) ainda está válido.
        (end < Utc(2026, 10, 22, 2, 0)).Should().BeFalse();
    }

    [Fact]
    public void End_PurchaseLateAtNightBrt_UsesBrasiliaDateNotUtcDate()
    {
        // 21/09 às 23:30 BRT já é 22/09 em UTC — o último dia conta a partir da data em Brasília.
        var end = PackageValidity.End(Utc(2026, 9, 22, 2, 30), 30);

        end.Should().Be(Utc(2026, 10, 22, 2, 59, 59)); // 21/10 23:59:59 BRT
    }

    [Fact]
    public void ExtendValidity_LegacyEndDateAtPurchaseTime_MovesToEndOfExtendedDay()
    {
        var sp = StudentPackage.Create(Guid.NewGuid(), Guid.NewGuid(), 30);
        typeof(StudentPackage).GetProperty(nameof(StudentPackage.EndDate))!
            .SetValue(sp, Utc(2026, 10, 21, 19, 25)); // legado: 21/10 16:25 BRT

        sp.ExtendValidity(5);

        sp.EndDate.Should().Be(Utc(2026, 10, 27, 2, 59, 59)); // 26/10 23:59:59 BRT
    }

    [Fact]
    public void ExtendValidity_EndOfDayEndDate_StaysAtEndOfDay()
    {
        var sp = StudentPackage.Create(Guid.NewGuid(), Guid.NewGuid(), 30);
        var before = sp.EndDate;

        sp.ExtendValidity(7);

        sp.EndDate.Should().Be(before.AddDays(7));
    }

    [Fact]
    public void Create_EndDateIsAlways235959Brasilia()
    {
        var sp = StudentPackage.Create(Guid.NewGuid(), Guid.NewGuid(), 30);

        var brt = sp.EndDate.AddHours(-3); // Brasília não tem horário de verão desde 2019
        brt.TimeOfDay.Should().Be(new TimeSpan(23, 59, 59));
        DateOnly.FromDateTime(brt).Should().Be(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)).AddDays(30));
    }

    [Fact]
    public void ChargeRenewed_ChainsFromPreviousEnd_KeepsEndOfDay()
    {
        var package = Package.Create("Essence (Recorrente)", null, credits: 8, price: 199.90m,
            validityDays: 30, popular: false, active: true, displayOrder: 0);
        var sp = StudentPackage.Create(Guid.NewGuid(), package.Id, 30);
        typeof(StudentPackage).GetProperty(nameof(StudentPackage.Package))!.SetValue(sp, package);
        var previousEnd = sp.EndDate;

        sp.ChargeRenewed();

        sp.EndDate.Should().Be(previousEnd.AddDays(30));
    }
}
