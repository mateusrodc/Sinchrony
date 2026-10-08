using Sinchrony.Domain.Enums;

namespace Sinchrony.Domain.Entities;

// Regra de recorrência semanal (ex.: seg/qua/sex 14:45, de 06/10 a 30/11). As aulas em si são
// linhas normais de `classes` geradas no cadastro e ligadas por Class.SeriesId; a série guarda só
// os padrões usados para gerar/editar/estender as ocorrências.
public class ClassSeries
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public Guid ClassTypeId { get; private set; }
    public Guid TeacherId { get; private set; }
    public Guid StudioId { get; private set; }
    public string StartTime { get; private set; } = string.Empty;
    public int Duration { get; private set; }
    public int TotalSpots { get; private set; }

    // Máscara de bits: bit N ligado = dia da semana N (0 = domingo … 6 = sábado).
    public int DaysOfWeekMask { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public ClassSeriesStatus Status { get; private set; } = ClassSeriesStatus.active;

    // Idempotência do cadastro: o front gera um id ao abrir o formulário; reenviar não duplica.
    public Guid RequestId { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public ClassType? ClassType { get; private set; }
    public User? Teacher { get; private set; }
    public Studio? Studio { get; private set; }
    public ICollection<Class> Occurrences { get; private set; } = [];

    public IReadOnlyList<int> DaysOfWeek => ToDays(DaysOfWeekMask);

    protected ClassSeries() { }

    public static ClassSeries Create(string name, Guid classTypeId, Guid teacherId, Guid studioId,
        string startTime, int duration, int totalSpots, IEnumerable<int> daysOfWeek,
        DateOnly startDate, DateOnly endDate, Guid requestId, Guid createdById)
        => new()
        {
            Name = name,
            ClassTypeId = classTypeId,
            TeacherId = teacherId,
            StudioId = studioId,
            StartTime = startTime,
            Duration = duration,
            TotalSpots = totalSpots,
            DaysOfWeekMask = ToMask(daysOfWeek),
            StartDate = startDate,
            EndDate = endDate,
            RequestId = requestId,
            CreatedById = createdById
        };

    public void UpdateDefaults(string name, Guid classTypeId, Guid teacherId, Guid studioId,
        string startTime, int duration, int totalSpots)
    {
        Name = name; ClassTypeId = classTypeId; TeacherId = teacherId; StudioId = studioId;
        StartTime = startTime; Duration = duration; TotalSpots = totalSpots;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ExtendTo(DateOnly endDate)
    {
        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        Status = ClassSeriesStatus.cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public static int ToMask(IEnumerable<int> days)
        => days.Distinct().Aggregate(0, (mask, d) => mask | (1 << d));

    public static IReadOnlyList<int> ToDays(int mask)
        => Enumerable.Range(0, 7).Where(d => (mask & (1 << d)) != 0).ToList();
}
