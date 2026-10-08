using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Infrastructure.Persistence.Repositories;

public class ClassSeriesRepository(ApplicationDbContext db) : IClassSeriesRepository
{
    public async Task<ClassSeries?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ClassSeries
            .Include(s => s.Studio)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ClassSeries?> GetByRequestIdAsync(Guid requestId, CancellationToken ct = default)
        => await db.ClassSeries
            .Include(s => s.Studio)
            .FirstOrDefaultAsync(s => s.RequestId == requestId, ct);

    public async Task<ClassSeries?> GetWithOccurrencesAsync(Guid id, CancellationToken ct = default)
    {
        var series = await GetByIdAsync(id, ct);
        if (series is null) return null;

        // Carrega as ocorrências (e reservas ativas) já ligadas à série rastreada pelo fixup do EF.
        await db.Classes
            .Include(c => c.Teacher)
            .Include(c => c.Bookings.Where(b => b.Status != BookingStatus.cancelled))
            .Where(c => c.SeriesId == id)
            .LoadAsync(ct);

        return series;
    }

    public async Task<int> CountOccurrencesAsync(Guid seriesId, CancellationToken ct = default)
        => await db.Classes.CountAsync(c => c.SeriesId == seriesId, ct);

    public async Task<HashSet<DateOnly>> ListSeriesDatesAsync(
        Guid seriesId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => (await db.Classes
                .Where(c => c.SeriesId == seriesId && c.SeriesDate >= from && c.SeriesDate <= to)
                .Select(c => c.SeriesDate!.Value)
                .ToListAsync(ct))
            .ToHashSet();

    public async Task AddAsync(ClassSeries series, CancellationToken ct = default)
        => await db.ClassSeries.AddAsync(series, ct);

    public async Task SaveAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
