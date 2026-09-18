using FluentAssertions;
using Sinchrony.Domain.Entities;
using Xunit;
using UnitEntity = Sinchrony.Domain.Entities.Unit;

namespace Sinchrony.Tests.Unit.Entities;

// DEMANDA_EXCLUIR_CADASTROS_ERP_BACKEND.md — "excluir" = desativar (soft-delete) para PackageType,
// ClassType e Unit, sem precisar reenviar o objeto inteiro via PUT.
public class ActivationTests
{
    [Fact]
    public void PackageType_DeactivateThenActivate_TogglesActiveAndKeepsOtherFields()
    {
        var pt = PackageType.Create("Mensal", isFamily: true, rank: 2);

        pt.Deactivate();
        pt.Active.Should().BeFalse();

        pt.Activate();
        pt.Active.Should().BeTrue();
        pt.Name.Should().Be("Mensal");
        pt.IsFamily.Should().BeTrue();
        pt.Rank.Should().Be(2);
    }

    [Fact]
    public void ClassType_DeactivateThenActivate_TogglesActiveAndKeepsFlags()
    {
        var ct = ClassType.Create("Ciclismo");
        ct.Update("Ciclismo", active: true, usesBikes: true);

        ct.Deactivate();
        ct.Active.Should().BeFalse();

        ct.Activate();
        ct.Active.Should().BeTrue();
        ct.UsesBikes.Should().BeTrue();
    }

    [Fact]
    public void Unit_DeactivateThenActivate_TogglesActiveAndKeepsData()
    {
        var unit = UnitEntity.Create("Palmas", "Rua A", "63999990000", "palmas@test.com");

        unit.Deactivate();
        unit.Active.Should().BeFalse();

        unit.Activate();
        unit.Active.Should().BeTrue();
        unit.Name.Should().Be("Palmas");
        unit.Email.Should().Be("palmas@test.com");
    }
}
