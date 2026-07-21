namespace Sinchrony.Domain.Entities;

public class Benefit
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public ICollection<PackageBenefit> PackageBenefits { get; private set; } = [];

    protected Benefit() { }

    public static Benefit Create(string name, string? description = null, string? icon = null)
        => new() { Name = name, Description = description, Icon = icon };

    public void Update(string name, string? description, string? icon, bool active)
    {
        Name = name;
        Description = description;
        Icon = icon;
        Active = active;
    }
}