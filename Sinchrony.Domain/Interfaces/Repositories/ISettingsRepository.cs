using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface ISettingsRepository
{
    Task<Settings?> GetAsync(CancellationToken ct = default);
    Task AddAsync(Settings settings, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}