namespace Sinchrony.Domain.Entities;

public class Unit
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool Active { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; private set; } = [];
    public ICollection<Studio> Studios { get; private set; } = [];

    protected Unit() { }

    public static Unit Create(string name, string? address = null,
        string? phone = null, string? email = null)
        => new() { Name = name, Address = address, Phone = phone, Email = email };

    public void Update(string name, string? address, string? phone, string? email, bool active)
    {
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        Active = active;
        UpdatedAt = DateTime.UtcNow;
    }
}