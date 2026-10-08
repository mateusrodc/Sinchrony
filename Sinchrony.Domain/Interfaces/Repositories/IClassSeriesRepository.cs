using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public interface IClassSeriesRepository
{
    // Sem ocorrências; traz Studio para checagem de unidade.
    Task<ClassSeries?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClassSeries?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default);

    // Série com todas as ocorrências (rastreadas), cada uma com as reservas não canceladas,
    // professor e sala. Ordenadas por SeriesDate.
    Task<ClassSeries?> GetWithOccurrencesAsync(Guid id, CancellationToken ct = default);

    Task<int> CountOccurrencesAsync(Guid seriesId, CancellationToken ct = default);

    // Datas originais (SeriesDate) já ocupadas por ocorrências da série, inclusive canceladas.
    Task<HashSet<DateOnly>> ListSeriesDatesAsync(Guid seriesId, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task AddAsync(ClassSeries series, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
