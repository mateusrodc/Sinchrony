using Xunit;
using FluentAssertions;
using Moq;
using Sinchrony.Domain.Entities;
using DomainUnit = Sinchrony.Domain.Entities.Unit;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Services;

namespace Sinchrony.Tests.Unit.Services;

public class SoleUnitTests
{
    private static Mock<IUnitRepository> Repo(params DomainUnit[] units)
    {
        var repo = new Mock<IUnitRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(units);
        return repo;
    }

    [Fact]
    public async Task SingleActiveUnit_IsReturned()
    {
        var unit = DomainUnit.Create("Unica", null, null, null);
        (await Repo(unit).Object.GetSoleActiveUnitIdAsync()).Should().Be(unit.Id);
    }

    [Fact]
    public async Task TwoActiveUnits_ReturnsNull()
    {
        var repo = Repo(DomainUnit.Create("A", null, null, null), DomainUnit.Create("B", null, null, null));
        (await repo.Object.GetSoleActiveUnitIdAsync()).Should().BeNull();
    }

    [Fact]
    public async Task InactiveUnitsAreIgnored()
    {
        var inactive = DomainUnit.Create("Antiga", null, null, null);
        inactive.Deactivate();
        var active = DomainUnit.Create("Atual", null, null, null);
        (await Repo(inactive, active).Object.GetSoleActiveUnitIdAsync()).Should().Be(active.Id);
    }
}
