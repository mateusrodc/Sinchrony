using Sinchrony.Domain.Exceptions;

namespace Sinchrony.Application.Packages;

public static class PackageTermsRulesValidator
{
    public static void Validate(
        int? freeNoShowsPerPackage, int? minimumCycles, decimal? earlyCancellationFeePercent, bool isRecurring)
    {
        if (freeNoShowsPerPackage is < 0)
            throw DomainException.Validation("INVALID_FREE_NO_SHOWS",
                "FreeNoShowsPerPackage não pode ser negativo.");

        if (minimumCycles is < 1)
            throw DomainException.Validation("INVALID_MINIMUM_CYCLES",
                "MinimumCycles deve ser pelo menos 1.");

        if (earlyCancellationFeePercent is < 0 or > 100)
            throw DomainException.Validation("INVALID_CANCELLATION_FEE_PERCENT",
                "EarlyCancellationFeePercent deve estar entre 0 e 100.");

        if (!isRecurring && (minimumCycles.HasValue || earlyCancellationFeePercent.HasValue))
            throw DomainException.Validation("RECURRING_ONLY_RULE",
                "Permanência mínima e multa de cancelamento só valem para pacote recorrente.");
    }
}
