namespace Sinchrony.Domain.Entities;

public class PackageType
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public bool Active { get; private set; } = true;
    public bool IsFamily { get; private set; }
    public int? Rank { get; private set; }
    public int? DefaultMaxFutureBookings { get; private set; }
    public int? DefaultMaxBookingsPerDay { get; private set; }
    public int? DefaultMaxBookingsPerWeek { get; private set; }
    public int? DefaultMaxBookingsPerMonth { get; private set; }
    public int? DefaultCancellationDeadlineHours { get; private set; }
    public int? DefaultBookingWindowDays { get; private set; }
    public int? DefaultEarlyAccessHours { get; private set; }
    public bool? DefaultAllowWaitlist { get; private set; }
    public bool? DefaultReschedulingAllowed { get; private set; }
    public int? DefaultReschedulingDeadlineHours { get; private set; }
    public bool? DefaultNoShowCreditPenalty { get; private set; }
    public int? DefaultMaxNoShowsBeforeBlock { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public ICollection<Package> Packages { get; private set; } = [];

    protected PackageType() { }

    public static PackageType Create(string name, bool isFamily = false, int? rank = null)
        => new() { Name = name, IsFamily = isFamily, Rank = rank };

    public void Update(
        string name, bool active, bool isFamily, int? rank,
        int? defaultMaxFutureBookings, int? defaultMaxBookingsPerDay,
        int? defaultMaxBookingsPerWeek, int? defaultMaxBookingsPerMonth,
        int? defaultCancellationDeadlineHours, int? defaultBookingWindowDays,
        int? defaultEarlyAccessHours, bool? defaultAllowWaitlist,
        bool? defaultReschedulingAllowed, int? defaultReschedulingDeadlineHours,
        bool? defaultNoShowCreditPenalty, int? defaultMaxNoShowsBeforeBlock)
    {
        Name = name;
        Active = active;
        IsFamily = isFamily;
        Rank = rank;
        DefaultMaxFutureBookings = defaultMaxFutureBookings;
        DefaultMaxBookingsPerDay = defaultMaxBookingsPerDay;
        DefaultMaxBookingsPerWeek = defaultMaxBookingsPerWeek;
        DefaultMaxBookingsPerMonth = defaultMaxBookingsPerMonth;
        DefaultCancellationDeadlineHours = defaultCancellationDeadlineHours;
        DefaultBookingWindowDays = defaultBookingWindowDays;
        DefaultEarlyAccessHours = defaultEarlyAccessHours;
        DefaultAllowWaitlist = defaultAllowWaitlist;
        DefaultReschedulingAllowed = defaultReschedulingAllowed;
        DefaultReschedulingDeadlineHours = defaultReschedulingDeadlineHours;
        DefaultNoShowCreditPenalty = defaultNoShowCreditPenalty;
        DefaultMaxNoShowsBeforeBlock = defaultMaxNoShowsBeforeBlock;
        UpdatedAt = DateTime.UtcNow;
    }
}