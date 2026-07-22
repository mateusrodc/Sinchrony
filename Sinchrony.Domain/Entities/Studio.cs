namespace Sinchrony.Domain.Entities;

public class Studio
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool Active { get; private set; } = true;
    public int Capacity { get; private set; }
    public string OpeningTime { get; private set; } = "06:00";
    public string ClosingTime { get; private set; } = "22:00";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public ICollection<Bike> Bikes { get; private set; } = [];
    public ICollection<Class> Classes { get; private set; } = [];

    public Guid? UnitId { get; private set; }

    public Unit? Unit { get; private set; }

    public void SetUnit(Guid? unitId)
    {
        UnitId = unitId;
    }

    protected Studio() { }

    public static Studio Create(string name, string address, int capacity,
        string? phone = null, string? email = null,
        string openingTime = "06:00", string closingTime = "22:00")
        => new()
        {
            Name = name,
            Address = address,
            Capacity = capacity,
            Phone = phone,
            Email = email,
            OpeningTime = openingTime,
            ClosingTime = closingTime
        };

    public void Update(string name, string address, int capacity,
        string? phone, string? email, string openingTime, string closingTime, bool active)
    {
        Name = name; Address = address; Capacity = capacity;
        Phone = phone; Email = email;
        OpeningTime = openingTime; ClosingTime = closingTime;
        Active = active;
    }
}