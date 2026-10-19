using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Services;

public record EarlyCancellationFee(
    int? MinimumCycles,
    int PaidCycles,
    int RemainingCycles,
    decimal? FeePercent,
    decimal CycleAmount,
    decimal FeeAmount);

// Termos v4, cl. 5.4: cancelar um plano recorrente promocional antes da permanência mínima
// sujeita o aluno a multa de X% sobre a soma das mensalidades restantes até cumpri-la.
public static class EarlyCancellationFeeCalculator
{
    public static EarlyCancellationFee Calculate(Package package, int paidCycles)
    {
        var minimum = package.MinimumCycles;
        var remaining = minimum.HasValue ? Math.Max(0, minimum.Value - paidCycles) : 0;
        var percent = package.EarlyCancellationFeePercent;
        var fee = percent.HasValue
            ? Math.Round(percent.Value / 100m * package.Price * remaining, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new EarlyCancellationFee(minimum, paidCycles, remaining, percent, package.Price, fee);
    }
}
