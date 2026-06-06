using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;

namespace Sinchrony.Infrastructure.Services;

public class AuditService(IAuditLogRepository repository) : IAuditService
{
    public async Task LogAsync(string @event, string entity,
        Guid? entityId = null, Guid? userId = null,
        string? details = null, string? ipAddress = null,
        CancellationToken ct = default)
    {
        var log = AuditLog.Create(@event, entity, entityId, userId, details, ipAddress);
        await repository.AddAsync(log, ct);
        await repository.SaveAsync(ct);
    }
}