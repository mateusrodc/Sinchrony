namespace Sinchrony.Domain.Entities;

public class WaitlistEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public int Position { get; private set; }
    public DateTime EnteredAt { get; private set; } = DateTime.UtcNow;
    public string Status { get; private set; } = "waiting"; // waiting | notified | expired | converted
    public DateTime? NotifiedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    public Class? Class { get; private set; }
    public User? Student { get; private set; }

    protected WaitlistEntry() { }

    public static WaitlistEntry Create(Guid classId, Guid studentId, int position)
        => new() { ClassId = classId, StudentId = studentId, Position = position };

    public void Notify()
    {
        Status = "notified";
        NotifiedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddMinutes(5); // janela de 5 minutos (Cláusula 8.3)
    }

    public void MarkExpired() => Status = "expired";
    public void MarkConverted() => Status = "converted";
}