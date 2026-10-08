using FluentValidation;
using FluentValidation.Results;
using Sinchrony.Application.Classes;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Scheduling;

namespace Sinchrony.Application.Series;

// Aulas recorrentes: uma série (regra semanal) + ocorrências individuais em `classes`, todas geradas
// no cadastro. Cada ocorrência é uma Class normal (reserva, presença, fila e relatórios não mudam).
public class ClassSeriesService(
    ClassPlanning planning,
    IClassSeriesRepository seriesRepository,
    IClassRepository classRepository,
    IUnitOfWork unitOfWork,
    IAuditService auditService)
{
    public const int MaxOccurrences = 200;

    private sealed record Planned(DateOnly Date, bool Excluded, List<ClassConflictDto> Conflicts);

    // ---------- Prévia ----------

    public async Task<SeriesPreviewDto> PreviewAsync(ClassSeriesInput input, CancellationToken ct)
    {
        var plan = await PlanAsync(input, ct);
        return ToPreview(plan);
    }

    // ---------- Criação ----------

    public async Task<SeriesCreateResult> CreateAsync(
        ClassSeriesInput input, Guid requestId, Guid adminId, CancellationToken ct)
    {
        if (requestId == Guid.Empty)
            throw Invalid("RequestId", "requestId é obrigatório.");

        ClassSeries series;
        int created;
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Serializa reenvios simultâneos do mesmo requestId: o segundo espera e enxerga a série pronta.
            await unitOfWork.AcquireAdvisoryLockAsync($"class-series:{requestId}", ct);

            var existing = await seriesRepository.GetByRequestIdAsync(requestId, ct);
            if (existing is not null)
            {
                if (!planning.CanManage(existing.Studio))
                    throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");
                var count = await seriesRepository.CountOccurrencesAsync(existing.Id, ct);
                await unitOfWork.CommitAsync(ct);
                return new SeriesCreateResult(new SeriesCreatedDto(ClassSeriesDto.From(existing), count), false);
            }

            // Serializa criações do mesmo professor: duas requisições distintas não passam juntas pela checagem.
            await unitOfWork.AcquireAdvisoryLockAsync(ClassPlanning.TeacherLockKey(input.TeacherId), ct);

            var plan = await PlanAsync(input, ct);
            ThrowIfConflicts(plan);

            var parsed = Parse(input);
            series = ClassSeries.Create(input.Name.Trim(), input.ClassTypeId, input.TeacherId, input.StudioId,
                input.StartTime, input.Duration, input.TotalSpots, input.DaysOfWeek,
                parsed.Start, parsed.End, requestId, adminId);

            var endTime = ClassSchedule.ComputeEndTime(input.StartTime, input.Duration);
            var occurrences = plan.Where(p => !p.Excluded)
                .Select(p => Class.CreateOccurrence(series, p.Date, endTime))
                .ToList();

            await seriesRepository.AddAsync(series, ct);
            await classRepository.AddRangeAsync(occurrences, ct);
            await seriesRepository.SaveAsync(ct);
            await unitOfWork.CommitAsync(ct);
            created = occurrences.Count;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }

        await auditService.LogAsync("class_series.created", "ClassSeries", series.Id, adminId,
            $"Name: {series.Name} Occurrences: {created}", ct: ct);

        return new SeriesCreateResult(new SeriesCreatedDto(ClassSeriesDto.From(series), created), true);
    }

    // ---------- Detalhe ----------

    public async Task<SeriesDetailDto> GetAsync(Guid id, Guid callerId, bool isAdmin, CancellationToken ct)
    {
        var series = await seriesRepository.GetWithOccurrencesAsync(id, ct)
            ?? throw DomainException.NotFound("Class series not found.");

        if (isAdmin)
        {
            if (!planning.CanManage(series.Studio))
                throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");
        }
        else if (series.TeacherId != callerId && series.Occurrences.All(c => c.TeacherId != callerId))
        {
            throw DomainException.NotFound("Class series not found.");
        }

        var occurrences = series.Occurrences
            .OrderBy(c => c.Date).ThenBy(c => c.StartTime)
            .Select(c => new SeriesOccurrenceDto(
                c.Id, Fmt(c.Date), Fmt(c.SeriesDate ?? c.Date), c.StartTime, c.EndTime,
                c.Status.ToString(), c.IsException, c.ActiveBookingsCount, c.Teacher?.Name))
            .ToList();

        return new SeriesDetailDto(ClassSeriesDto.From(series), occurrences);
    }

    // ---------- Edição em grupo ("esta e as próximas" / "toda a série") ----------

    public async Task<SeriesUpdateResultDto> UpdateOccurrencesAsync(
        Guid id, DateOnly from, ClassSeriesChanges changes, Guid adminId, CancellationToken ct)
    {
        var series = await seriesRepository.GetWithOccurrencesAsync(id, ct)
            ?? throw DomainException.NotFound("Class series not found.");
        if (!planning.CanManage(series.Studio))
            throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");
        if (series.Status == ClassSeriesStatus.cancelled)
            throw DomainException.Conflict("SERIES_CANCELLED", "A série está cancelada.");

        if (changes.Name is null && changes.ClassTypeId is null && changes.TeacherId is null
            && changes.StudioId is null && changes.StartTime is null && changes.Duration is null
            && changes.TotalSpots is null)
            throw Invalid("Changes", "Informe ao menos um campo para alterar.");

        var name = changes.Name?.Trim() ?? series.Name;
        var classTypeId = changes.ClassTypeId ?? series.ClassTypeId;
        var teacherId = changes.TeacherId ?? series.TeacherId;
        var studioId = changes.StudioId ?? series.StudioId;
        var startTime = changes.StartTime ?? series.StartTime;
        var duration = changes.Duration ?? series.Duration;
        var totalSpots = changes.TotalSpots ?? series.TotalSpots;

        (await new ClassFieldsValidator().ValidateAsync(new ClassFields(name, startTime, duration, totalSpots), ct))
            .ThrowIfInvalid();

        if (studioId != series.StudioId) await planning.RequireManageableStudioAsync(studioId, ct);
        await planning.RequireReferencesAsync(classTypeId, teacherId, ct);

        var today = planning.Today;
        var endTime = ClassSchedule.ComputeEndTime(startTime, duration);

        var targets = series.Occurrences
            .Where(c => (c.SeriesDate ?? c.Date) >= from)
            .OrderBy(c => c.SeriesDate ?? c.Date)
            .ToList();

        var candidatesCache = new Dictionary<(Guid Studio, Guid Teacher), IReadOnlyList<Class>>();
        var skipped = new List<SeriesSkippedDto>();
        var updated = 0;

        foreach (var c in targets)
        {
            var active = c.ActiveBookingsCount;
            string? reason = null;

            // Efetivo desta ocorrência: o que mudou de fato em relação ao que ela tem hoje.
            var newName = changes.Name is null ? c.Name : name;
            var newType = changes.ClassTypeId ?? c.ClassTypeId;
            var newTeacher = changes.TeacherId ?? c.TeacherId;
            var newStudio = changes.StudioId ?? c.StudioId;
            var newStart = changes.StartTime ?? c.StartTime;
            var newDuration = changes.Duration ?? c.Duration;
            var newSpots = changes.TotalSpots ?? c.TotalSpots;
            var newEnd = ClassSchedule.ComputeEndTime(newStart, newDuration);

            var timingChanged = newStart != c.StartTime || newDuration != c.Duration || newStudio != c.StudioId;
            var slotChanged = timingChanged || newTeacher != c.TeacherId;

            if (c.Date < today) reason = SkipReason.Past;
            else if (c.Status != ClassStatus.scheduled) reason = SkipReason.NotScheduled;
            else if (c.IsException) reason = SkipReason.Exception;
            else if (active > 0 && timingChanged) reason = SkipReason.HasBookings;
            else if (newSpots < active) reason = SkipReason.SpotsBelowBookings;
            else if (slotChanged)
            {
                var key = (newStudio, newTeacher);
                if (!candidatesCache.TryGetValue(key, out var candidates))
                {
                    var min = targets.Min(t => t.Date);
                    var max = targets.Max(t => t.Date);
                    candidates = await classRepository.ListSchedulingCandidatesAsync(min, max, newStudio, newTeacher, ct);
                    candidatesCache[key] = candidates;
                }
                if (ClassConflictChecker.Blocking(
                        ClassConflictChecker.Find(c.Date, newStart, newEnd, newStudio, newTeacher, candidates, c.Id)).Count > 0)
                    reason = SkipReason.Conflict;
            }

            if (reason is not null)
            {
                skipped.Add(new SeriesSkippedDto(c.Id, Fmt(c.Date), reason, active));
                continue;
            }

            c.Update(newName, newType, newTeacher, newStudio, c.Date, newStart, newEnd,
                newDuration, newSpots, c.Status);
            updated++;
        }

        series.UpdateDefaults(name, classTypeId, teacherId, studioId, startTime, duration, totalSpots);
        await seriesRepository.SaveAsync(ct);

        await auditService.LogAsync("class_series.updated", "ClassSeries", series.Id, adminId,
            $"From: {Fmt(from)} Updated: {updated} Skipped: {skipped.Count}", ct: ct);

        return new SeriesUpdateResultDto(updated, skipped);
    }

    // ---------- Cancelamento em grupo ----------

    public async Task<SeriesCancelResultDto> CancelAsync(Guid id, DateOnly from, Guid adminId, CancellationToken ct)
    {
        var series = await seriesRepository.GetWithOccurrencesAsync(id, ct)
            ?? throw DomainException.NotFound("Class series not found.");
        if (!planning.CanManage(series.Studio))
            throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");

        var today = planning.Today;
        var skipped = new List<SeriesSkippedDto>();
        var cancelled = 0;

        var targets = series.Occurrences
            .Where(c => (c.SeriesDate ?? c.Date) >= from && c.Date >= today && c.Status == ClassStatus.scheduled)
            .OrderBy(c => c.Date);

        foreach (var c in targets)
        {
            var active = c.ActiveBookingsCount;
            if (active > 0)
            {
                // Cancelar aula com reservas exigiria devolver crédito e avisar o aluno (não existe).
                skipped.Add(new SeriesSkippedDto(c.Id, Fmt(c.Date), SkipReason.HasBookings, active));
                continue;
            }
            c.Cancel();
            cancelled++;
        }

        if (!series.Occurrences.Any(c => c.Date >= today && c.Status == ClassStatus.scheduled))
            series.MarkCancelled();

        await seriesRepository.SaveAsync(ct);

        await auditService.LogAsync("class_series.cancelled", "ClassSeries", series.Id, adminId,
            $"From: {Fmt(from)} Cancelled: {cancelled} Skipped: {skipped.Count}", ct: ct);

        return new SeriesCancelResultDto(cancelled, skipped, series.Status.ToString());
    }

    // ---------- Estender ----------

    // dryRun = true devolve só a prévia (nada é gravado); false grava e devolve a série e o total criado.
    public async Task<SeriesExtendResult> ExtendAsync(
        Guid id, string endDate, IReadOnlyList<string>? excludedDates, bool dryRun, Guid adminId, CancellationToken ct)
    {
        if (dryRun) return await ExtendCoreAsync(id, endDate, excludedDates, true, ct);

        SeriesExtendResult result;
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Duas extensões simultâneas da mesma série: a segunda espera, relê o fim atual e recebe o
            // 422 limpo ("endDate deve ser posterior ao fim atual") em vez de estourar o índice único.
            await unitOfWork.AcquireAdvisoryLockAsync($"class-series-extend:{id}", ct);
            result = await ExtendCoreAsync(id, endDate, excludedDates, false, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }

        await auditService.LogAsync("class_series.extended", "ClassSeries", id, adminId,
            $"EndDate: {result.Created!.Series.EndDate} Occurrences: {result.Created.Created}", ct: ct);

        return result;
    }

    private async Task<SeriesExtendResult> ExtendCoreAsync(
        Guid id, string endDate, IReadOnlyList<string>? excludedDates, bool dryRun, CancellationToken ct)
    {
        var series = await seriesRepository.GetByIdAsync(id, ct)
            ?? throw DomainException.NotFound("Class series not found.");
        if (!planning.CanManage(series.Studio))
            throw DomainException.Forbidden("Você não tem permissão para gerenciar esta unidade.");
        if (series.Status == ClassSeriesStatus.cancelled)
            throw DomainException.Conflict("SERIES_CANCELLED", "A série está cancelada.");

        if (!ClassSeriesInputValidator.TryDate(endDate, out var newEnd))
            throw Invalid("EndDate", "endDate deve estar no formato yyyy-MM-dd.");
        if (newEnd <= series.EndDate)
            throw Invalid("EndDate", "endDate deve ser posterior ao fim atual da série.");
        if (newEnd > series.EndDate.AddMonths(ClassSeriesInputValidator.MaxMonths))
            throw Invalid("EndDate", $"A extensão pode ter no máximo {ClassSeriesInputValidator.MaxMonths} meses por vez.");

        var excluded = ParseExcluded(excludedDates);

        // Só datas depois do fim atual (e não passadas). A chave (SeriesId, SeriesDate) garante que nada
        // existente — nem cancelada — seja recriado; a checagem aqui evita estourar o índice.
        var first = series.EndDate.AddDays(1);
        if (first < planning.Today) first = planning.Today;
        var taken = await seriesRepository.ListSeriesDatesAsync(series.Id, first, newEnd, ct);
        var dates = ClassSchedule.GenerateDates(series.DaysOfWeek, first, newEnd)
            .Where(d => !taken.Contains(d)).ToList();

        // Gravando: serializa com outras criações do mesmo professor antes de checar conflito.
        if (!dryRun)
            await unitOfWork.AcquireAdvisoryLockAsync(ClassPlanning.TeacherLockKey(series.TeacherId), ct);

        var plan = await BuildPlanAsync(dates, excluded, series.StudioId, series.TeacherId,
            series.StartTime, series.Duration, ct);

        if (dryRun) return new SeriesExtendResult(ToPreview(plan), null);

        ThrowIfConflicts(plan);

        var endTime = ClassSchedule.ComputeEndTime(series.StartTime, series.Duration);
        var occurrences = plan.Where(p => !p.Excluded)
            .Select(p => Class.CreateOccurrence(series, p.Date, endTime))
            .ToList();

        await classRepository.AddRangeAsync(occurrences, ct);
        series.ExtendTo(newEnd);
        await seriesRepository.SaveAsync(ct);

        return new SeriesExtendResult(null, new SeriesCreatedDto(ClassSeriesDto.From(series), occurrences.Count));
    }

    // ---------- Planejamento ----------

    private sealed record ParsedInput(DateOnly Start, DateOnly End, HashSet<DateOnly> Excluded);

    private ParsedInput Parse(ClassSeriesInput input)
        => new(DateOnly.ParseExact(input.StartDate, "yyyy-MM-dd"),
               DateOnly.ParseExact(input.EndDate, "yyyy-MM-dd"),
               ParseExcluded(input.ExcludedDates));

    private static HashSet<DateOnly> ParseExcluded(IReadOnlyList<string>? values)
    {
        var set = new HashSet<DateOnly>();
        foreach (var v in values ?? [])
        {
            if (!ClassSeriesInputValidator.TryDate(v, out var d))
                throw Invalid("ExcludedDates", "excludedDates deve conter datas no formato yyyy-MM-dd.");
            set.Add(d);
        }
        return set;
    }

    private async Task<List<Planned>> PlanAsync(ClassSeriesInput input, CancellationToken ct)
    {
        (await new ClassSeriesInputValidator(planning.Today).ValidateAsync(input, ct)).ThrowIfInvalid();

        await planning.RequireManageableStudioAsync(input.StudioId, ct);
        await planning.RequireReferencesAsync(input.ClassTypeId, input.TeacherId, ct);

        var parsed = Parse(input);
        var dates = ClassSchedule.GenerateDates(input.DaysOfWeek, parsed.Start, parsed.End);
        return await BuildPlanAsync(dates, parsed.Excluded, input.StudioId, input.TeacherId,
            input.StartTime, input.Duration, ct);
    }

    private async Task<List<Planned>> BuildPlanAsync(
        IReadOnlyList<DateOnly> dates, HashSet<DateOnly> excluded, Guid studioId, Guid teacherId,
        string startTime, int duration, CancellationToken ct)
    {
        var active = dates.Count(d => !excluded.Contains(d));
        if (active == 0)
            throw Invalid("ExcludedDates", "A série precisa ter pelo menos uma aula depois das exclusões.");
        if (active > MaxOccurrences)
            throw Invalid("EndDate", $"A série pode ter no máximo {MaxOccurrences} aulas.");

        var endTime = ClassSchedule.ComputeEndTime(startTime, duration);
        var candidates = await classRepository.ListSchedulingCandidatesAsync(
            dates.Min(), dates.Max(), studioId, teacherId, ct);

        var byDate = candidates.ToLookup(c => c.Date);
        return dates.Select(d => new Planned(d, excluded.Contains(d),
                excluded.Contains(d)
                    ? []
                    : ClassConflictChecker.Find(d, startTime, endTime, studioId, teacherId, byDate[d])))
            .ToList();
    }

    private static SeriesPreviewDto ToPreview(IReadOnlyList<Planned> plan)
        => new(
            plan.Count(p => !p.Excluded),
            plan.Count(p => !p.Excluded && HasBlocking(p)),
            plan.Count(p => !p.Excluded && !HasBlocking(p) && p.Conflicts.Count > 0),
            plan.Select(p => new SeriesPreviewOccurrenceDto(Fmt(p.Date), (int)p.Date.DayOfWeek, p.Excluded, p.Conflicts))
                .ToList());

    private static void ThrowIfConflicts(IReadOnlyList<Planned> plan)
    {
        if (!plan.Any(p => !p.Excluded && HasBlocking(p))) return;
        throw DomainException.Conflict("SERIES_HAS_CONFLICTS",
            "O professor já tem aula em algumas datas. Exclua essas datas e envie novamente.",
            new Dictionary<string, object?> { ["occurrences"] = ToPreview(plan).Occurrences });
    }

    private static bool HasBlocking(Planned p) => p.Conflicts.Any(c => c.Type == ClassConflictChecker.TeacherType);

    private static string Fmt(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static ValidationException Invalid(string field, string message)
        => new([new ValidationFailure(field, message)]);
}

internal static class ValidationResultExtensions
{
    public static void ThrowIfInvalid(this ValidationResult result)
    {
        if (!result.IsValid) throw new ValidationException(result.Errors);
    }
}
