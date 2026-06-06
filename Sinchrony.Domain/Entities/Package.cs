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

    public ICollection<Purchase> Purchases { get; private set; } = [];

    protected Package() { }

    public static Package Create(string name, string? description, int credits, decimal price,
        int validityDays, bool popular, int displayOrder)
        => new()
        {
            Name = name,
            Description = description,
            Credits = credits,
            Price = price,
            PricePerCredit = credits > 0 ? Math.Round(price / credits, 2) : 0,
            ValidityDays = validityDays,
            Popular = popular,
            DisplayOrder = displayOrder
        };

    public void Update(string name, string? description, int credits, decimal price,
        int validityDays, bool popular, bool active, int displayOrder)
    {
        Name = name; Description = description; Credits = credits; Price = price;
        PricePerCredit = credits > 0 ? Math.Round(price / credits, 2) : 0;
        ValidityDays = validityDays; Popular = popular; Active = active; DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Toggle() { Active = !Active; UpdatedAt = DateTime.UtcNow; }
}