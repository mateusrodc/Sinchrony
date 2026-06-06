namespace Sinchrony.Domain.Interfaces.Services;

public interface IAuditService
{
    Task LogAsync(string @event, string entity,
        Guid? entityId = null, Guid? userId = null,
        string? details = null, string? ipAddress = null,
        CancellationToken ct = default);
}