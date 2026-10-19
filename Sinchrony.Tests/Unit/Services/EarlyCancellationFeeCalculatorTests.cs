using Xunit;
using FluentAssertions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Services;

namespace Sinchrony.Tests.Unit.Services;

public class EarlyCancellationFeeCalculatorTests
{
    private static Package Promo(int? minimumCycles, decimal? percent)
    {
        var p = Package.Create("Promo", null, 8, 200m, 30, false, true, 1);
        p.SetTermsRules(null, minimumCycles, percent);
        return p;
    }

    [Fact]
    public void OneCyclePaidOfThree_ChargesTenPercentOfTwoRemaining()
    {
        var fee = EarlyCancellationFeeCalculator.Calculate(Promo(3, 10m), paidCycles: 1);
        fee.RemainingCycles.Should().Be(2);
        fee.FeeAmount.Should().Be(40m);
    }

    [Fact]
    public void MinimumReached_NoFee()
    {
        var fee = EarlyCancellationFeeCalculator.Calculate(Promo(3, 10m), paidCycles: 3);
        fee.RemainingCycles.Should().Be(0);
        fee.FeeAmount.Should().Be(0m);
    }

    [Fact]
    public void PackageWithoutRules_NoFee()
    {
        var fee = EarlyCancellationFeeCalculator.Calculate(Promo(null, null), paidCycles: 1);
        fee.FeeAmount.Should().Be(0m);
    }
}
