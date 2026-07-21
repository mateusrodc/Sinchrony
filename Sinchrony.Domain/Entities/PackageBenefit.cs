namespace Sinchrony.Domain.Entities;

public class PackageBenefit
{
    public Guid PackageId { get; private set; }
    public Guid BenefitId { get; private set; }

    public Package? Package { get; private set; }
    public Benefit? Benefit { get; private set; }

    protected PackageBenefit() { }

    public static PackageBenefit Create(Guid packageId, Guid benefitId)
        => new() { PackageId = packageId, BenefitId = benefitId };
}