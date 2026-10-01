using FluentAssertions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// DEMANDA_BACKEND_DEMANDAS_PENDENTES item 5: valor da aula por modalidade (com padrão), bônus por
// aluno presente, ambos com vigência por data — o relatório de agosto sai com os valores de agosto.
public class TeacherRateResolverTests
{
    private static readonly Guid Spinning = Guid.NewGuid();
    private static readonly Guid JiuAutista = Guid.NewGuid();

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static TeacherRateResolver Resolver(params ClassRate[] rates) =>
        new(rates, [TeacherBonusRate.Create(3m, D(2026, 1, 1), null)]);

    [Fact]
    public void DefaultRate_AppliesToAnyClassType_WithoutOwnRow()
    {
        var resolver = Resolver(ClassRate.Create(null, 65m, D(2026, 1, 1), null));

        resolver.ResolveClassValue(Spinning, D(2026, 8, 10)).Should().Be(65m);
    }

    [Fact]
    public void PriceChange_IsVersioned_AugustKeepsOldValue_SeptemberUsesNew()
    {
        var resolver = Resolver(
            ClassRate.Create(null, 65m, D(2026, 1, 1), null),
            ClassRate.Create(null, 70m, D(2026, 9, 1), null));

        resolver.ResolveClassValue(Spinning, D(2026, 8, 31)).Should().Be(65m);
        resolver.ResolveClassValue(Spinning, D(2026, 9, 1)).Should().Be(70m);
        resolver.ResolveClassValue(Spinning, D(2026, 9, 30)).Should().Be(70m);
    }

    [Fact]
    public void ClassTypeRow_WinsOverDefault()
    {
        var resolver = Resolver(
            ClassRate.Create(null, 65m, D(2026, 1, 1), null),
            ClassRate.Create(JiuAutista, 75m, D(2026, 1, 1), null));

        resolver.ResolveClassValue(JiuAutista, D(2026, 8, 10)).Should().Be(75m);
        resolver.ResolveClassValue(Spinning, D(2026, 8, 10)).Should().Be(65m);
    }

    [Fact]
    public void ClassTypeRow_NotYetEffective_FallsBackToDefault()
    {
        var resolver = Resolver(
            ClassRate.Create(null, 65m, D(2026, 1, 1), null),
            ClassRate.Create(JiuAutista, 75m, D(2026, 9, 1), null));

        resolver.ResolveClassValue(JiuAutista, D(2026, 8, 31)).Should().Be(65m);
        resolver.ResolveClassValue(JiuAutista, D(2026, 9, 1)).Should().Be(75m);
    }

    [Fact]
    public void NoRateAtAll_ResolvesToZero()
    {
        var resolver = new TeacherRateResolver([], []);

        resolver.ResolveClassValue(Spinning, D(2026, 8, 10)).Should().Be(0m);
        resolver.ResolveBonusPerStudent(D(2026, 8, 10)).Should().Be(0m);
    }

    [Fact]
    public void Bonus_IsPerAttendedStudent_AndVersioned()
    {
        var resolver = new TeacherRateResolver(
            [],
            [
                TeacherBonusRate.Create(3m, D(2026, 1, 1), null),
                TeacherBonusRate.Create(4m, D(2026, 10, 1), null)
            ]);

        // Aula com 4 presentes em agosto: bônus de R$ 12,00
        (4 * resolver.ResolveBonusPerStudent(D(2026, 8, 20))).Should().Be(12m);
        resolver.ResolveBonusPerStudent(D(2026, 10, 1)).Should().Be(4m);
    }

    [Fact]
    public void SameEffectiveFrom_LatestCreatedWins()
    {
        var first = ClassRate.Create(null, 60m, D(2026, 1, 1), null);
        Thread.Sleep(5);
        var corrected = ClassRate.Create(null, 65m, D(2026, 1, 1), null);

        Resolver(first, corrected).ResolveClassValue(Spinning, D(2026, 8, 10)).Should().Be(65m);
    }
}
