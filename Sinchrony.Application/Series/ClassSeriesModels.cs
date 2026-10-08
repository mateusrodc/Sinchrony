using FluentValidation;
using Sinchrony.Application.Classes;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Scheduling;

namespace Sinchrony.Application.Series;

// Corpo comum de prévia e criação. Datas em yyyy-MM-dd (calendário local, sem fuso).
public record ClassSeriesInput(
    string Name, Guid ClassTypeId, Guid TeacherId, Guid StudioId,
    string StartTime, int Duration, int TotalSpots,
    IReadOnlyList<int> DaysOfWeek, string StartDate, string EndDate,
    IReadOnlyList<string>? ExcludedDates);

// Só os campos que mudam; nulo = não mexer.
public record ClassSeriesChanges(
    string? Name, Guid? ClassTypeId, Guid? TeacherId, Guid? StudioId,
    string? StartTime, int? Duration, int? TotalSpots);

public record ClassSeriesDto(
    Guid Id, string Name, Guid ClassTypeId, Guid TeacherId, Guid StudioId,
    string StartTime, int Duration, int TotalSpots, IReadOnlyList<int> DaysOfWeek,
    string StartDate, string EndDate, string Status)
{
    public static ClassSeriesDto From(ClassSeries s) => new(
        s.Id, s.Name, s.ClassTypeId, s.TeacherId, s.StudioId, s.StartTime, s.Duration, s.TotalSpots,
        s.DaysOfWeek, s.StartDate.ToString("yyyy-MM-dd"), s.EndDate.ToString("yyyy-MM-dd"), s.Status.ToString());
}

public record SeriesPreviewOccurrenceDto(string Date, int Weekday, bool Excluded, IReadOnlyList<ClassConflictDto> Conflicts);

public record SeriesPreviewDto(int Total, int ConflictsCount, IReadOnlyList<SeriesPreviewOccurrenceDto> Occurrences);

public record SeriesCreatedDto(ClassSeriesDto Series, int Created);

public record SeriesOccurrenceDto(
    Guid ClassId, string Date, string SeriesDate, string StartTime, string EndTime, string Status,
    bool IsException, int ActiveBookings, string? TeacherName);

public record SeriesDetailDto(ClassSeriesDto Series, IReadOnlyList<SeriesOccurrenceDto> Occurrences);

public static class SkipReason
{
    public const string Past = "PAST";
    public const string NotScheduled = "NOT_SCHEDULED";
    public const string Exception = "EXCEPTION";
    public const string HasBookings = "HAS_BOOKINGS";
    public const string SpotsBelowBookings = "SPOTS_BELOW_BOOKINGS";
    public const string Conflict = "CONFLICT";
}

public record SeriesSkippedDto(Guid ClassId, string Date, string Reason, int ActiveBookings);

public record SeriesUpdateResultDto(int Updated, IReadOnlyList<SeriesSkippedDto> Skipped);

public record SeriesCancelResultDto(int Cancelled, IReadOnlyList<SeriesSkippedDto> Skipped, string SeriesStatus);

// Resultado do cadastro: IsNew = false quando o requestId já tinha série (idempotência → 200).
public record SeriesCreateResult(SeriesCreatedDto Data, bool IsNew);

public record SeriesExtendResult(SeriesPreviewDto? Preview, SeriesCreatedDto? Created);

internal sealed class ClassSeriesInputValidator : AbstractValidator<ClassSeriesInput>
{
    public ClassSeriesInputValidator(DateOnly today)
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.ClassTypeId).NotEmpty();
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.StudioId).NotEmpty();
        RuleFor(x => x.StartTime).ValidStartTime();
        RuleFor(x => x.Duration).ValidDuration().EndsSameDay(x => x.StartTime);
        RuleFor(x => x.TotalSpots).ValidTotalSpots();

        RuleFor(x => x.DaysOfWeek)
            .NotEmpty().WithMessage("Selecione pelo menos um dia da semana.")
            .Must(d => d is null || d.All(v => v is >= 0 and <= 6)).WithMessage("daysOfWeek deve conter valores de 0 (domingo) a 6 (sábado).");

        RuleFor(x => x.StartDate).ValidDate("startDate");
        RuleFor(x => x.EndDate).ValidDate("endDate");
        RuleForEach(x => x.ExcludedDates).ValidDate("excludedDates");

        When(x => TryDate(x.StartDate, out _), () =>
            RuleFor(x => x.StartDate)
                .Must(v => TryDate(v, out var d) && d >= today).WithMessage("startDate não pode ser anterior a hoje."));

        When(x => TryDate(x.StartDate, out _) && TryDate(x.EndDate, out _), () =>
        {
            RuleFor(x => x.EndDate)
                .Must((x, v) => DateOnly.ParseExact(v, "yyyy-MM-dd") >= DateOnly.ParseExact(x.StartDate, "yyyy-MM-dd"))
                .WithMessage("endDate não pode ser anterior a startDate.");
            RuleFor(x => x.EndDate)
                .Must((x, v) => DateOnly.ParseExact(v, "yyyy-MM-dd")
                    <= DateOnly.ParseExact(x.StartDate, "yyyy-MM-dd").AddMonths(MaxMonths))
                .WithMessage($"A série pode ter no máximo {MaxMonths} meses.");
        });
    }

    internal const int MaxMonths = 6;

    internal static bool TryDate(string? v, out DateOnly d)
        => DateOnly.TryParseExact(v, "yyyy-MM-dd", out d);
}

// Campos de aula resultantes de uma edição em grupo (padrões da série + mudanças pedidas).
internal sealed record ClassFields(string Name, string StartTime, int Duration, int TotalSpots);

internal sealed class ClassFieldsValidator : AbstractValidator<ClassFields>
{
    public ClassFieldsValidator()
    {
        RuleFor(x => x.Name).ValidName();
        RuleFor(x => x.StartTime).ValidStartTime();
        RuleFor(x => x.Duration).ValidDuration().EndsSameDay(x => x.StartTime);
        RuleFor(x => x.TotalSpots).ValidTotalSpots();
    }
}
