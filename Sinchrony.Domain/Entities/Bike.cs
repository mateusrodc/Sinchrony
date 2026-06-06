using Sinchrony.Domain.Enums;

namespace Sinchrony.Domain.Entities;

public class Bike
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid StudioId { get; private set; }
    public int Number { get; private set; }
    public BikeStatus Status { get; private set; } = BikeStatus.available;
    public DateOnly? LastMaintenance { get; private set; }
    public string? Notes { get; private set; }

    public Studio? Studio { get; private set; }

    protected Bike() { }

    public static Bike Create(Guid studioId, int number)
        => new() { StudioId = studioId, Number = number };

    public void Update(BikeStatus status, string? notes, DateOnly? lastMaintenance)
    {
        Status = status; Notes = notes; LastMaintenance = lastMaintenance;
    }
}