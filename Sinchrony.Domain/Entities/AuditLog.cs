namespace Sinchrony.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Event { get; private set; } = string.Empty;
    public string Entity { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Details { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    protected AuditLog() { }

    public static AuditLog Create(
        string @event, string entity,
        Guid? entityId = null, Guid? userId = null,
        string? details = null, string? ipAddress = null)
        => new()
        {
            Event = @event,
            Entity = entity,
            EntityId = entityId,
            UserId = userId,
            Details = details,
            IpAddress = ipAddress
        };
}