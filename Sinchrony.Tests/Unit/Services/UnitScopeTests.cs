using Xunit;
using FluentAssertions;
using Moq;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;

namespace Sinchrony.Tests.Unit.Services;

public class UnitScopeTests
{
    private readonly Mock<IUnitContext> _ctx = new();
    private readonly Guid _unitA = Guid.NewGuid();
    private readonly Guid _unitB = Guid.NewGuid();

    private void Setup(bool global, Guid? userUnitId, params Guid[] managed)
    {
        _ctx.SetupGet(c => c.IsGlobalAdmin).Returns(global);
        _ctx.SetupGet(c => c.UnitId).Returns(userUnitId);
        _ctx.Setup(c => c.GetManagedUnitIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(managed);
    }

    [Fact]
    public async Task GlobalAdmin_HasNoRestriction()
    {
        Setup(global: true, null);
        (await _ctx.Object.GetScopeAsync()).Should().BeNull();
        await _ctx.Object.EnsureCanAccessUnitAsync(_unitA); // não lança
    }

    // O caso da secretária: Role admin, sem User.UnitId, unidade marcada só em "Unidades (filiais)".
    [Fact]
    public async Task AdminWithoutUserUnitButWithBranch_SeesOnlyThatUnit()
    {
        Setup(global: false, null, _unitA);

        (await _ctx.Object.GetScopeAsync()).Should().BeEquivalentTo([_unitA]);
        await _ctx.Object.EnsureCanAccessUnitAsync(_unitA);

        var other = async () => await _ctx.Object.EnsureCanAccessUnitAsync(_unitB);
        (await other.Should().ThrowAsync<DomainException>()).Which.Should().Match<DomainException>(
            e => e.Code == "UNIT_FORBIDDEN" && e.HttpStatus == 403);
    }

    [Fact]
    public async Task AdminWithoutAnyUnit_GetsNoUnitAssigned()
    {
        Setup(global: false, null);

        var act = async () => await _ctx.Object.GetScopeAsync();
        (await act.Should().ThrowAsync<DomainException>()).Which.Should().Match<DomainException>(
            e => e.Code == "NO_UNIT_ASSIGNED" && e.HttpStatus == 403);
    }

    [Fact]
    public async Task RecordWithoutUnit_IsVisibleToAnyAdmin()
    {
        Setup(global: false, null, _unitA);
        await _ctx.Object.EnsureCanAccessUnitAsync(null);
    }

    [Fact]
    public async Task DefaultUnit_IsTheOnlyManagedUnit()
    {
        Setup(global: false, null, _unitA);
        (await _ctx.Object.ResolveDefaultUnitIdAsync()).Should().Be(_unitA);

        Setup(global: false, null, _unitA, _unitB);
        (await _ctx.Object.ResolveDefaultUnitIdAsync()).Should().BeNull();

        Setup(global: false, _unitB, _unitA, _unitB);
        (await _ctx.Object.ResolveDefaultUnitIdAsync()).Should().Be(_unitB);
    }
}
