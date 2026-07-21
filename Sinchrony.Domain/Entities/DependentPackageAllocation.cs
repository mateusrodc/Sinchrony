namespace Sinchrony.Domain.Entities;

public class DependentPackageAllocation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StudentPackageId { get; private set; }
    public Guid? DependentId { get; private set; } // null = titular
    public int CreditsRemaining { get; private set; }

    public StudentPackage? StudentPackage { get; private set; }
    public Dependent? Dependent { get; private set; }

    protected DependentPackageAllocation() { }

    public static DependentPackageAllocation Create(
        Guid studentPackageId, Guid? dependentId, int credits)
        => new()
        {
            StudentPackageId = studentPackageId,
            DependentId = dependentId,
            CreditsRemaining = credits
        };

    public void Debit(int amount)
    {
        if (amount > CreditsRemaining)
            throw new InvalidOperationException("Insufficient credits in allocation.");
        CreditsRemaining -= amount;
    }

    public void Credit(int amount) => CreditsRemaining += amount;
}