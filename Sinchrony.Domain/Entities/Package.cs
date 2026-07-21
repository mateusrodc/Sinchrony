namespace Sinchrony.Domain.Entities;

public class Package
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Credits { get; private set; }
    public decimal Price { get; private set; }
    public decimal PricePerCredit { get; private set; }
    public int ValidityDays { get; private set; }
    public bool Popular { get; private set; }
    public bool Active { get; private set; } = true;
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    // Campos novos — adicione à entidade existente
    public Guid PackageTypeId { get; private set; }
    public int MaxDependents { get; private set; }
    public int? CreditsPerMember { get; private set; }
    public string PurchaseStrategy { get; private set; } = "block";

    // Regras específicas (nível 1 da cascata)
    public int? MaxFutureBookings { get; private set; }
    public int? MaxBookingsPerDay { get; private set; }
    public int? MaxBookingsPerWeek { get; private set; }
    public int? MaxBookingsPerMonth { get; private set; }
    public int? CancellationDeadlineHours { get; private set; }
    public int? BookingWindowDays { get; private set; }
    public int? EarlyAccessHours { get; private set; }
    public bool? AllowWaitlist { get; private set; }
    public int? WaitlistPriority { get; private set; }
    public bool? ReschedulingAllowed { get; private set; }
    public int? ReschedulingDeadlineHours { get; private set; }
    public bool NoShowCreditPenalty { get; private set; } = true;
    public int? MaxNoShowsBeforeBlock { get; private set; }
    public int NoShowBlockWindowDays { get; private set; } = 30;

    public PackageType? PackageType { get; private set; }
    public ICollection<PackageBenefit> PackageBenefits { get; private set; } = [];

    public ICollection<Purchase> Purchases { get; private set; } = [];

    protected Package() { }

    public static Package Create(
    string name, string? description, int credits, decimal price,
    int validityDays, bool popular, bool active, int displayOrder,
    Guid packageTypeId, string purchaseStrategy = "block",
    int maxDependents = 0)
    {
        return new Package
        {
            Name = name,
            Description = description,
            Credits = credits,
            Price = price,
            ValidityDays = validityDays,
            Popular = popular,
            Active = active,
            DisplayOrder = displayOrder,
            PackageTypeId = packageTypeId,
            PurchaseStrategy = purchaseStrategy,
            MaxDependents = maxDependents
        };
    }

    public void Update(string name, string? description, int credits, decimal price,
        int validityDays, bool popular, bool active, int displayOrder)
    {
        Name = name; Description = description; Credits = credits; Price = price;
        PricePerCredit = credits > 0 ? Math.Round(price / credits, 2) : 0;
        ValidityDays = validityDays; Popular = popular; Active = active; DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateFull(
    string name, string? description, int credits, decimal price,
    int validityDays, bool popular, bool active, int displayOrder,
    Guid packageTypeId, string purchaseStrategy,
    int maxDependents, int? creditsPerMember,
    int? maxFutureBookings, int? maxBookingsPerDay,
    int? maxBookingsPerWeek, int? maxBookingsPerMonth,
    int? cancellationDeadlineHours, int? bookingWindowDays,
    int? earlyAccessHours, bool? allowWaitlist, int? waitlistPriority,
    bool? reschedulingAllowed, int? reschedulingDeadlineHours,
    bool noShowCreditPenalty, int? maxNoShowsBeforeBlock, int noShowBlockWindowDays)
    {
        Name = name;
        Description = description;
        Credits = credits;
        Price = price;
        ValidityDays = validityDays;
        Popular = popular;
        Active = active;
        DisplayOrder = displayOrder;
        PackageTypeId = packageTypeId;
        PurchaseStrategy = purchaseStrategy;
        MaxDependents = maxDependents;
        CreditsPerMember = creditsPerMember;
        MaxFutureBookings = maxFutureBookings;
        MaxBookingsPerDay = maxBookingsPerDay;
        MaxBookingsPerWeek = maxBookingsPerWeek;
        MaxBookingsPerMonth = maxBookingsPerMonth;
        CancellationDeadlineHours = cancellationDeadlineHours;
        BookingWindowDays = bookingWindowDays;
        EarlyAccessHours = earlyAccessHours;
        AllowWaitlist = allowWaitlist;
        WaitlistPriority = waitlistPriority;
        ReschedulingAllowed = reschedulingAllowed;
        ReschedulingDeadlineHours = reschedulingDeadlineHours;
        NoShowCreditPenalty = noShowCreditPenalty;
        MaxNoShowsBeforeBlock = maxNoShowsBeforeBlock;
        NoShowBlockWindowDays = noShowBlockWindowDays;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Toggle() { Active = !Active; UpdatedAt = DateTime.UtcNow; }
}