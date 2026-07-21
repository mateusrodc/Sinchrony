namespace Sinchrony.Domain.Entities;

public enum StudentPackageStatus { active, queued, expired, cancelled }

public class StudentPackage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StudentId { get; private set; }
    public Guid PackageId { get; private set; }
    public StudentPackageStatus Status { get; private set; } = StudentPackageStatus.active;
    public DateTime PurchasedAt { get; private set; } = DateTime.UtcNow;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }

    public User? Student { get; private set; }
    public Package? Package { get; private set; }
    public ICollection<DependentPackageAllocation> Allocations { get; private set; } = [];

    protected StudentPackage() { }

    public static StudentPackage Create(Guid studentId, Guid packageId, int validityDays)
    {
        var start = DateTime.UtcNow;
        return new StudentPackage
        {
            StudentId = studentId,
            PackageId = packageId,
            StartDate = start,
            EndDate = start.AddDays(validityDays),
            Status = StudentPackageStatus.active
        };
    }

    public static StudentPackage CreateQueued(Guid studentId, Guid packageId, int validityDays)
    {
        var sp = Create(studentId, packageId, validityDays);
        sp.Status = StudentPackageStatus.queued;
        return sp;
    }

    public bool IsExpired() => EndDate < DateTime.UtcNow;

    public void Expire()
    {
        Status = StudentPackageStatus.expired;
    }

    public void Cancel()
    {
        Status = StudentPackageStatus.cancelled;
    }

    public void Activate()
    {
        Status = StudentPackageStatus.active;
        StartDate = DateTime.UtcNow;
        EndDate = StartDate.AddDays((Package?.ValidityDays) ?? 30);
    }
    public void ExtendValidity(int days)
    {
        EndDate = EndDate.AddDays(days);
    }
}