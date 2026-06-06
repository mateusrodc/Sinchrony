using Sinchrony.Domain.Enums;

namespace Sinchrony.Domain.Entities;

public class ClassSession
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClassId { get; private set; }
    public ClassStatus Status { get; private set; } = ClassStatus.in_progress;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; private set; }
    public int Duration { get; private set; }

    public Class? Class { get; private set; }

    protected ClassSession() { }

    public static ClassSession Create(Guid classId, int duration)
        => new() { ClassId = classId, Duration = duration };

    public void End() { Status = ClassStatus.completed; EndedAt = DateTime.UtcNow; }
}