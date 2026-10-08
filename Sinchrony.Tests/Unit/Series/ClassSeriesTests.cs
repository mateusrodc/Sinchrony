using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Application.Classes;
using Sinchrony.Application.Classes.Commands.CreateClass;
using Sinchrony.Application.Classes.Commands.UpdateClass;
using Sinchrony.Application.Series;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Scheduling;
using Xunit;

namespace Sinchrony.Tests.Unit.Series;

// DEMANDA_AULAS_RECORRENTES_BACKEND.md — séries semanais + ocorrências em `classes`.
public class ClassScheduleTests
{
    [Fact]
    public void GenerateDates_IncludesStartAndEnd_AndCrossesMonthAndYear()
    {
        // 28/12/2026 (seg) a 08/01/2027 (sex), seg/qua/sex.
        var dates = ClassSchedule.GenerateDates([1, 3, 5], new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 8));

        dates.Should().Equal(
            new DateOnly(2026, 12, 28), new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 6), new DateOnly(2027, 1, 8));
    }

    [Fact]
    public void GenerateDates_Sunday_IsZero()
    {
        var dates = ClassSchedule.GenerateDates([0], new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 18));

        dates.Should().Equal(new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 18));
    }

    [Theory]
    [InlineData("14:45", 45, "15:30")]
    [InlineData("07:00", 60, "08:00")]
    [InlineData("23:00", 59, "23:59")]
    public void ComputeEndTime_AddsDuration(string start, int duration, string expected)
        => ClassSchedule.ComputeEndTime(start, duration).Should().Be(expected);

    [Fact]
    public void Overlaps_TouchingClassesDoNotConflict()
    {
        ClassSchedule.Overlaps("14:00", "15:00", "15:00", "16:00").Should().BeFalse();
        ClassSchedule.Overlaps("14:00", "15:00", "14:59", "16:00").Should().BeTrue();
        ClassSchedule.Overlaps("14:30", "15:30", "14:45", "15:30").Should().BeTrue();
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("7:00")]
    [InlineData("07:60")]
    [InlineData("ab:cd")]
    public void TryParseTime_RejectsInvalid(string value)
        => ClassSchedule.TryParseTime(value, out _).Should().BeFalse();

    [Fact]
    public void ClassSeries_DaysMask_RoundTrips()
        => ClassSeries.ToDays(ClassSeries.ToMask([5, 1, 3, 1])).Should().Equal(1, 3, 5);
}

public class ClassSeriesCreationTests
{
    private readonly SeriesFixture _f = new();

    [Fact]
    public async Task Create_MonWedFri_Generates24Occurrences_WithOwnIds_SeriesIdAndSeriesDate()
    {
        var result = await _f.CreateAsync();

        result.IsNew.Should().BeTrue();
        result.Data.Created.Should().Be(24);
        var classes = await _f.OccurrencesAsync(result.Data.Series.Id);
        classes.Should().HaveCount(24);
        classes.Select(c => c.Id).Distinct().Should().HaveCount(24);
        classes.First().Date.Should().Be(new DateOnly(2026, 10, 7)); // 06/10 é terça
        classes.Should().OnlyContain(c => c.SeriesId == result.Data.Series.Id && c.SeriesDate == c.Date
            && c.Status == ClassStatus.scheduled && !c.IsException);
        classes.Should().OnlyContain(c => c.StartTime == "14:45" && c.EndTime == "15:30");
        result.Data.Series.DaysOfWeek.Should().Equal(1, 3, 5);
    }

    [Fact]
    public async Task Create_SameRequestIdTwice_ReturnsExistingSeries_WithoutGeneratingAgain()
    {
        var requestId = Guid.NewGuid();

        var first = await _f.CreateAsync(requestId: requestId);
        var second = await _f.CreateAsync(requestId: requestId);

        second.IsNew.Should().BeFalse();
        second.Data.Series.Id.Should().Be(first.Data.Series.Id);
        second.Data.Created.Should().Be(24);
        (await _f.Db.ClassSeries.CountAsync()).Should().Be(1);
        (await _f.Db.Classes.CountAsync()).Should().Be(24);
    }

    [Fact]
    public async Task Create_WithExcludedDates_SkipsThem()
    {
        var result = await _f.CreateAsync(_f.Input(excluded: ["2026-11-02"]));

        result.Data.Created.Should().Be(23);
        (await _f.OccurrencesAsync(result.Data.Series.Id)).Should().NotContain(c => c.Date == new DateOnly(2026, 11, 2));
    }

    [Fact]
    public async Task Preview_MarksStudioConflict_AndListsExcludedDates()
    {
        _f.AddClass(new DateOnly(2026, 10, 9), "14:30", "15:15", studioId: _f.Studio.Id);

        var preview = await _f.Service.PreviewAsync(_f.Input(excluded: ["2026-11-02"]), default);

        preview.Total.Should().Be(23);
        preview.ConflictsCount.Should().Be(1);
        preview.Occurrences.Should().HaveCount(24);
        var conflicted = preview.Occurrences.Single(o => o.Date == "2026-10-09");
        conflicted.Weekday.Should().Be(5);
        conflicted.Conflicts.Should().ContainSingle(c => c.Type == "studio" && c.StartTime == "14:30");
        preview.Occurrences.Single(o => o.Date == "2026-11-02").Excluded.Should().BeTrue();
        (await _f.Db.ClassSeries.CountAsync()).Should().Be(0); // prévia não grava
    }

    [Fact]
    public async Task Create_WithConflict_Returns409AndSavesNothing_ThenSucceedsWhenExcluded()
    {
        _f.AddClass(new DateOnly(2026, 10, 9), "14:30", "15:15", studioId: _f.Studio.Id);

        var act = () => _f.CreateAsync();

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        ex.Code.Should().Be("SERIES_HAS_CONFLICTS");
        ex.HttpStatus.Should().Be(409);
        ex.Extensions.Should().ContainKey("occurrences");
        (await _f.Db.ClassSeries.CountAsync()).Should().Be(0);
        (await _f.Db.Classes.CountAsync()).Should().Be(1);

        var ok = await _f.CreateAsync(_f.Input(excluded: ["2026-10-09"]));
        ok.Data.Created.Should().Be(23);
    }

    [Fact]
    public async Task Create_TeacherConflictInAnotherRoom_Blocks()
    {
        _f.AddClass(new DateOnly(2026, 10, 7), "15:00", "15:45", teacherId: _f.Teacher.Id, studioId: _f.OtherStudio.Id);

        var act = () => _f.CreateAsync();

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        var occ = ex.Extensions!["occurrences"] as IEnumerable<SeriesPreviewOccurrenceDto>;
        occ!.Single(o => o.Date == "2026-10-07").Conflicts.Should().ContainSingle(c => c.Type == "teacher");
    }

    [Fact]
    public async Task Create_CancelledClassInSlot_DoesNotConflict()
    {
        _f.AddClass(new DateOnly(2026, 10, 9), "14:30", "15:15", studioId: _f.Studio.Id, status: ClassStatus.cancelled);

        var result = await _f.CreateAsync();

        result.Data.Created.Should().Be(24);
    }

    [Fact]
    public async Task Create_TouchingClass_DoesNotConflict()
    {
        _f.AddClass(new DateOnly(2026, 10, 9), "15:30", "16:15", studioId: _f.Studio.Id);

        var result = await _f.CreateAsync();

        result.Data.Created.Should().Be(24);
    }

    [Fact]
    public async Task Create_ExistingConflictInDatabase_OnlyBlocksNewOnes_NotExistingClasses()
    {
        // aulas já em conflito no banco continuam existindo; a validação só vale para o que se cria agora
        _f.AddClass(new DateOnly(2026, 12, 1), "10:00", "11:00", studioId: _f.Studio.Id);
        _f.AddClass(new DateOnly(2026, 12, 1), "10:30", "11:30", studioId: _f.Studio.Id);

        var result = await _f.CreateAsync();

        result.Data.Created.Should().Be(24);
        (await _f.Db.Classes.CountAsync()).Should().Be(26);
    }

    [Theory]
    [InlineData("2026-10-04", "2026-11-30", "StartDate")] // antes de hoje (05/10)
    [InlineData("2026-10-08", "2027-04-09", "EndDate")] // mais de 6 meses
    [InlineData("2026-10-20", "2026-10-10", "EndDate")] // fim antes do início
    public async Task Create_InvalidDates_Returns422WithField(string start, string end, string field)
    {
        var act = () => _f.CreateAsync(_f.Input(startDate: start, endDate: end));

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().Contain(e => e.PropertyName == field);
    }

    [Fact]
    public async Task Create_SixMonthsExactly_IsAllowed_AndRespectsTwoHundredCap()
    {
        // 08/10/2026 a 08/04/2027 = 6 meses; todos os dias da semana ~183 aulas (< 200)
        var result = await _f.CreateAsync(_f.Input(startDate: "2026-10-08", endDate: "2027-04-08", days: [0, 1, 2, 3, 4, 5, 6]));

        result.Data.Created.Should().BeLessThanOrEqualTo(ClassSeriesService.MaxOccurrences);
        result.Data.Created.Should().Be(183);
    }

    [Fact]
    public async Task Create_NoDays_Returns422()
    {
        var act = () => _f.CreateAsync(_f.Input(days: []));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "DaysOfWeek");
    }

    [Fact]
    public async Task Create_AllDatesExcluded_Returns422()
    {
        var act = () => _f.CreateAsync(_f.Input(startDate: "2026-10-12", endDate: "2026-10-12", days: [1], excluded: ["2026-10-12"]));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "ExcludedDates");
    }

    [Fact]
    public async Task Create_BadFormat_Returns422()
    {
        var act = () => _f.CreateAsync(_f.Input(startTime: "7h", duration: 5));

        var errors = (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Select(e => e.PropertyName);
        errors.Should().Contain(["StartTime", "Duration"]);
    }

    [Fact]
    public async Task Create_OtherUnitStudio_IsForbidden()
    {
        _f.UnitContext.SetupGet(u => u.IsGlobalAdmin).Returns(false);
        _f.UnitContext.SetupGet(u => u.UnitId).Returns(Guid.NewGuid());

        var act = () => _f.CreateAsync();

        (await act.Should().ThrowAsync<DomainException>()).Which.HttpStatus.Should().Be(403);
    }

    [Fact]
    public async Task Get_ReturnsOccurrences_WithBookingsAndTeacher_AndHidesFromOtherTeachers()
    {
        var created = await _f.CreateAsync();
        var first = (await _f.OccurrencesAsync(created.Data.Series.Id)).First();
        _f.Book(first);

        var detail = await _f.Service.GetAsync(created.Data.Series.Id, _f.AdminId, true, default);

        detail.Occurrences.Should().HaveCount(24);
        var occ = detail.Occurrences.Single(o => o.ClassId == first.Id);
        occ.ActiveBookings.Should().Be(1);
        occ.TeacherName.Should().Be("Prof");
        occ.SeriesDate.Should().Be("2026-10-07");

        (await _f.Service.GetAsync(created.Data.Series.Id, _f.Teacher.Id, false, default)).Occurrences.Should().HaveCount(24);
        var other = () => _f.Service.GetAsync(created.Data.Series.Id, _f.OtherTeacher.Id, false, default);
        (await other.Should().ThrowAsync<DomainException>()).Which.HttpStatus.Should().Be(404);
    }
}

public class ClassSeriesEditTests
{
    private readonly SeriesFixture _f = new();

    // 08/10 a 31/10, seg/qua/sex: 09,12,14,16,19,21,23,26,28,30/10 (10 aulas).
    private async Task<(ClassSeriesDto Series, List<Class> Classes)> CreateOctoberAsync()
    {
        var result = await _f.CreateAsync(_f.Input(startDate: "2026-10-08", endDate: "2026-10-31"));
        return (result.Data.Series, await _f.OccurrencesAsync(result.Data.Series.Id));
    }

    private static ClassSeriesChanges Change(string? name = null, Guid? teacherId = null, Guid? studioId = null,
        string? startTime = null, int? duration = null, int? spots = null)
        => new(name, null, teacherId, studioId, startTime, duration, spots);

    private Task<SeriesUpdateResultDto> UpdateAsync(Guid id, string from, ClassSeriesChanges changes)
        => _f.Service.UpdateOccurrencesAsync(id, DateOnly.Parse(from), changes, _f.AdminId, default);

    [Fact]
    public async Task Update_TeacherNameAndSpots_AppliesToOccurrencesFromDate_AndUpdatesSeriesDefaults()
    {
        var (series, classes) = await CreateOctoberAsync();

        var result = await UpdateAsync(series.Id, "2026-10-19", Change(name: "Pilates Mat", teacherId: _f.OtherTeacher.Id, spots: 12));

        result.Updated.Should().Be(6); // 19,21,23,26,28,30
        result.Skipped.Should().BeEmpty();
        var after = await _f.OccurrencesAsync(series.Id);
        after.Where(c => c.Date >= new DateOnly(2026, 10, 19)).Should()
            .OnlyContain(c => c.Name == "Pilates Mat" && c.TeacherId == _f.OtherTeacher.Id && c.TotalSpots == 12);
        after.Where(c => c.Date < new DateOnly(2026, 10, 19)).Should().OnlyContain(c => c.Name == "Pilates");
        var saved = await _f.Db.ClassSeries.SingleAsync();
        saved.Name.Should().Be("Pilates Mat");
        saved.TeacherId.Should().Be(_f.OtherTeacher.Id);
    }

    [Fact]
    public async Task Update_StartTime_RecomputesEndTime()
    {
        var (series, _) = await CreateOctoberAsync();

        await UpdateAsync(series.Id, "2026-10-08", Change(startTime: "16:00", duration: 60));

        (await _f.OccurrencesAsync(series.Id)).Should().OnlyContain(c => c.StartTime == "16:00" && c.EndTime == "17:00");
    }

    [Fact]
    public async Task Update_PastOccurrence_IsSkipped_WithPast()
    {
        var (series, classes) = await CreateOctoberAsync();
        // 16/10 já é "hoje" para este serviço: 09, 12 e 14 ficam no passado.
        var later = new ClassSeriesService(
            new ClassPlanning(new Infrastructure.Persistence.Repositories.StudioRepository(_f.Db),
                new Infrastructure.Persistence.Repositories.ClassTypeRepository(_f.Db),
                new Infrastructure.Persistence.Repositories.UserRepository(_f.Db), _f.Classes, _f.UnitContext.Object,
                new FixedClock(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero))),
            _f.SeriesRepo, _f.Classes, _f.UnitOfWork.Object, _f.Audit.Object);

        var result = await later.UpdateOccurrencesAsync(series.Id, new DateOnly(2026, 10, 1),
            Change(name: "Novo"), _f.AdminId, default);

        result.Skipped.Where(s => s.Reason == SkipReason.Past).Select(s => s.Date)
            .Should().Equal("2026-10-09", "2026-10-12", "2026-10-14");
        result.Updated.Should().Be(7);
    }

    [Fact]
    public async Task Update_NotScheduled_IsSkipped()
    {
        var (series, classes) = await CreateOctoberAsync();
        classes[1].Cancel();
        await _f.Db.SaveChangesAsync();

        var result = await UpdateAsync(series.Id, "2026-10-08", Change(name: "X"));

        result.Skipped.Should().ContainSingle(s => s.ClassId == classes[1].Id && s.Reason == SkipReason.NotScheduled);
        result.Updated.Should().Be(9);
    }

    [Fact]
    public async Task Update_Exception_IsSkipped_AfterIndividualEdit()
    {
        var (series, classes) = await CreateOctoberAsync();
        var target = classes[2];
        var handler = new UpdateClassCommandHandler(_f.Planning, _f.Classes, _f.Audit.Object);

        // PUT individual (muda o professor desta ocorrência) -> vira exceção
        await handler.Handle(new UpdateClassCommand(_f.AdminId, target.Id, target.Name, target.ClassTypeId,
            _f.OtherTeacher.Id, target.StudioId, target.Date.ToString("yyyy-MM-dd"), target.StartTime, null,
            target.Duration, target.TotalSpots, "scheduled"), default);
        (await _f.Db.Classes.AsNoTracking().SingleAsync(c => c.Id == target.Id)).IsException.Should().BeTrue();

        var result = await UpdateAsync(series.Id, "2026-10-08", Change(teacherId: _f.Teacher.Id, name: "Grupo"));

        result.Skipped.Should().ContainSingle(s => s.ClassId == target.Id && s.Reason == SkipReason.Exception);
        (await _f.Db.Classes.AsNoTracking().SingleAsync(c => c.Id == target.Id)).TeacherId.Should().Be(_f.OtherTeacher.Id);
    }

    [Fact]
    public async Task Update_TimeChange_SkipsOccurrencesWithBookings_ButTeacherChangeApplies()
    {
        var (series, classes) = await CreateOctoberAsync();
        _f.Book(classes[3]);
        _f.Book(classes[3]);

        var time = await UpdateAsync(series.Id, "2026-10-08", Change(startTime: "09:00"));

        time.Updated.Should().Be(9);
        time.Skipped.Should().ContainSingle(s => s.ClassId == classes[3].Id
            && s.Reason == SkipReason.HasBookings && s.ActiveBookings == 2);

        // professor/nome/vagas podem mudar mesmo com reservas
        var teacher = await UpdateAsync(series.Id, "2026-10-08", Change(teacherId: _f.OtherTeacher.Id, name: "Z", spots: 5));
        teacher.Skipped.Should().NotContain(s => s.Reason == SkipReason.HasBookings);
        (await _f.Db.Classes.AsNoTracking().SingleAsync(c => c.Id == classes[3].Id)).TeacherId.Should().Be(_f.OtherTeacher.Id);
    }

    [Fact]
    public async Task Update_SpotsBelowBookings_IsSkipped()
    {
        var (series, classes) = await CreateOctoberAsync();
        _f.Book(classes[0]);
        _f.Book(classes[0]);
        _f.Book(classes[0]);

        var result = await UpdateAsync(series.Id, "2026-10-08", Change(spots: 2));

        result.Skipped.Should().ContainSingle(s => s.ClassId == classes[0].Id && s.Reason == SkipReason.SpotsBelowBookings);
        result.Updated.Should().Be(9);
    }

    [Fact]
    public async Task Update_ConflictWithAnotherClass_IsSkipped()
    {
        var (series, classes) = await CreateOctoberAsync();
        // o novo professor já tem aula na ocorrência de 12/10 no mesmo horário
        _f.AddClass(new DateOnly(2026, 10, 12), "14:45", "15:30", teacherId: _f.OtherTeacher.Id);

        var result = await UpdateAsync(series.Id, "2026-10-08", Change(teacherId: _f.OtherTeacher.Id));

        result.Skipped.Should().ContainSingle(s => s.Date == "2026-10-12" && s.Reason == SkipReason.Conflict);
        result.Updated.Should().Be(9);
    }

    [Fact]
    public async Task Update_NothingToChange_Returns422()
    {
        var (series, _) = await CreateOctoberAsync();

        var act = () => UpdateAsync(series.Id, "2026-10-08", Change());

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Cancel_SkipsOccurrencesWithBookings_AndCancelsTheRest()
    {
        var (series, classes) = await CreateOctoberAsync();
        _f.Book(classes[5]);

        var result = await _f.Service.CancelAsync(series.Id, new DateOnly(2026, 10, 19), _f.AdminId, default);

        result.Cancelled.Should().Be(5); // 19,21,23,26,28,30 menos a reservada (21/10)
        result.Skipped.Should().ContainSingle(s => s.ClassId == classes[5].Id && s.Reason == SkipReason.HasBookings);
        result.SeriesStatus.Should().Be("active");
        var after = await _f.OccurrencesAsync(series.Id);
        after.Where(c => c.Date < new DateOnly(2026, 10, 19)).Should().OnlyContain(c => c.Status == ClassStatus.scheduled);
        after.Single(c => c.Id == classes[5].Id).Status.Should().Be(ClassStatus.scheduled);
    }

    [Fact]
    public async Task Cancel_WholeSeriesWithoutBookings_MarksSeriesCancelled()
    {
        var (series, _) = await CreateOctoberAsync();

        var result = await _f.Service.CancelAsync(series.Id, SeriesFixture.Today, _f.AdminId, default);

        result.Cancelled.Should().Be(10);
        result.SeriesStatus.Should().Be("cancelled");
        (await _f.OccurrencesAsync(series.Id)).Should().OnlyContain(c => c.Status == ClassStatus.cancelled);
    }

    [Fact]
    public async Task Cancel_IncludesOccurrencesMarkedAsException()
    {
        var (series, classes) = await CreateOctoberAsync();
        classes[0].MarkAsException();
        await _f.Db.SaveChangesAsync();

        var result = await _f.Service.CancelAsync(series.Id, SeriesFixture.Today, _f.AdminId, default);

        result.Cancelled.Should().Be(10);
    }

    [Fact]
    public async Task Extend_AddsOnlyDatesAfterCurrentEnd_AndNeverRecreatesCancelledOnes()
    {
        var (series, classes) = await CreateOctoberAsync();
        classes[1].Cancel(); // 12/10 cancelada
        await _f.Db.SaveChangesAsync();

        var result = await _f.Service.ExtendAsync(series.Id, "2026-11-15", null, false, _f.AdminId, default);

        result.Created!.Created.Should().Be(6); // 02,04,06,09,11,13/11
        result.Created.Series.EndDate.Should().Be("2026-11-15");
        var all = await _f.OccurrencesAsync(series.Id);
        all.Should().HaveCount(16);
        all.Select(c => c.SeriesDate).Distinct().Should().HaveCount(16);
        all.Single(c => c.Id == classes[1].Id).Status.Should().Be(ClassStatus.cancelled);
        all.Where(c => c.Date > new DateOnly(2026, 10, 31)).Should().OnlyContain(c => c.Date >= new DateOnly(2026, 11, 2));
    }

    [Fact]
    public async Task Extend_SkipsDatesThatAlreadyExistInTheSeries()
    {
        var (series, _) = await CreateOctoberAsync();
        // estende duas vezes sobre o mesmo trecho (a segunda só traz o que falta)
        await _f.Service.ExtendAsync(series.Id, "2026-11-06", null, false, _f.AdminId, default);
        var second = await _f.Service.ExtendAsync(series.Id, "2026-11-13", null, false, _f.AdminId, default);

        second.Created!.Created.Should().Be(3); // 09, 11 e 13/11
        (await _f.OccurrencesAsync(series.Id)).Should().HaveCount(16);
    }

    [Fact]
    public async Task Extend_DryRun_ReturnsPreviewAndWritesNothing()
    {
        var (series, _) = await CreateOctoberAsync();

        var result = await _f.Service.ExtendAsync(series.Id, "2026-11-15", ["2026-11-04"], true, _f.AdminId, default);

        result.Created.Should().BeNull();
        result.Preview!.Total.Should().Be(5);
        result.Preview.Occurrences.Should().HaveCount(6);
        (await _f.OccurrencesAsync(series.Id)).Should().HaveCount(10);
        (await _f.Db.ClassSeries.SingleAsync()).EndDate.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public async Task Extend_WithConflict_Returns409()
    {
        var (series, _) = await CreateOctoberAsync();
        _f.AddClass(new DateOnly(2026, 11, 2), "15:00", "15:45", studioId: _f.Studio.Id);

        var act = () => _f.Service.ExtendAsync(series.Id, "2026-11-15", null, false, _f.AdminId, default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SERIES_HAS_CONFLICTS");
        (await _f.OccurrencesAsync(series.Id)).Should().HaveCount(10);
    }

    [Theory]
    [InlineData("2026-10-31")] // não passa do fim atual
    [InlineData("2027-05-01")] // mais de 6 meses por chamada
    public async Task Extend_InvalidEnd_Returns422(string endDate)
    {
        var (series, _) = await CreateOctoberAsync();

        var act = () => _f.Service.ExtendAsync(series.Id, endDate, null, false, _f.AdminId, default);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Extend_CancelledSeries_Returns409()
    {
        var (series, _) = await CreateOctoberAsync();
        await _f.Service.CancelAsync(series.Id, SeriesFixture.Today, _f.AdminId, default);

        var act = () => _f.Service.ExtendAsync(series.Id, "2026-11-15", null, false, _f.AdminId, default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SERIES_CANCELLED");
    }

    [Fact]
    public async Task SeriesOccurrence_AppearsInTeacherReport_WithBookingCounts()
    {
        var (series, classes) = await CreateOctoberAsync();
        var booking = _f.Book(classes[0]);
        var record = AttendanceRecord.Create(booking.Id, classes[0].Id, booking.StudentId);
        record.UpdateStatus("attended", _f.Teacher.Id);
        _f.Db.AttendanceRecords.Add(record);
        await _f.Db.SaveChangesAsync();

        var rows = await _f.Classes.ListForTeacherReportAsync(
            new TeacherClassReportFilter(null, null, _f.Teacher.Id, null, null, null, null, null));

        rows.Should().HaveCount(10);
        rows.Single(r => r.ClassId == classes[0].Id).Attended.Should().Be(1);
    }
}

public class SingleClassRulesTests
{
    private readonly SeriesFixture _f = new();

    private CreateClassCommandHandler CreateHandler() => new(_f.Planning, _f.Classes, _f.Audit.Object);
    private UpdateClassCommandHandler UpdateHandler() => new(_f.Planning, _f.Classes, _f.Audit.Object);

    private CreateClassCommand Create(string date = "2026-10-20", string start = "10:00", int duration = 60,
        Guid? teacherId = null, Guid? studioId = null, string? endTime = "03:00")
        => new(_f.AdminId, "Yoga", _f.Type.Id, teacherId ?? _f.Teacher.Id, studioId ?? _f.Studio.Id,
            date, start, endTime, duration, 15);

    [Fact]
    public async Task Create_ComputesEndTime_IgnoringClientValue()
    {
        var created = await CreateHandler().Handle(Create(endTime: "03:00"), default);

        created.EndTime.Should().Be("11:00");
        created.SeriesId.Should().BeNull();
    }

    [Fact]
    public async Task Create_PastDate_Returns409ClassInPast()
    {
        var act = () => CreateHandler().Handle(Create(date: "2026-10-04"), default);

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        ex.Code.Should().Be("CLASS_IN_PAST");
        ex.HttpStatus.Should().Be(409);
    }

    [Fact]
    public async Task Create_Today_IsAllowed()
    {
        var created = await CreateHandler().Handle(Create(date: "2026-10-05"), default);

        created.Date.Should().Be(SeriesFixture.Today);
    }

    [Fact]
    public async Task Create_StudioConflict_Returns409WithConflictList()
    {
        _f.AddClass(new DateOnly(2026, 10, 20), "10:30", "11:30", studioId: _f.Studio.Id);

        var act = () => CreateHandler().Handle(Create(), default);

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        ex.Code.Should().Be("CLASS_CONFLICT");
        var conflicts = (IEnumerable<ClassConflictDto>)ex.Extensions!["conflicts"]!;
        conflicts.Should().ContainSingle(c => c.Type == "studio" && c.StartTime == "10:30" && c.Date == "2026-10-20");
    }

    [Fact]
    public async Task Create_TeacherConflictInOtherRoom_Returns409()
    {
        _f.AddClass(new DateOnly(2026, 10, 20), "10:30", "11:30", teacherId: _f.Teacher.Id, studioId: _f.OtherStudio.Id);

        var act = () => CreateHandler().Handle(Create(), default);

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        ((IEnumerable<ClassConflictDto>)ex.Extensions!["conflicts"]!).Should().ContainSingle(c => c.Type == "teacher");
    }

    [Fact]
    public async Task Create_InvalidFormats_Fail()
    {
        var validator = new CreateClassCommandValidator();

        (await validator.ValidateAsync(Create(start: "9h"))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create(duration: 10))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create(duration: 241))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create(start: "23:30", duration: 60))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create(date: "20/10/2026"))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create() with { TotalSpots = 0 })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create() with { TotalSpots = 201 })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(Create())).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Create_OtherUnitStudio_IsForbidden()
    {
        _f.UnitContext.SetupGet(u => u.IsGlobalAdmin).Returns(false);
        _f.UnitContext.SetupGet(u => u.UnitId).Returns(Guid.NewGuid());

        var act = () => CreateHandler().Handle(Create(), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.HttpStatus.Should().Be(403);
    }

    private UpdateClassCommand Update(Class c, Func<UpdateClassCommand, UpdateClassCommand>? tweak = null)
    {
        var cmd = new UpdateClassCommand(_f.AdminId, c.Id, c.Name, c.ClassTypeId, c.TeacherId, c.StudioId,
            c.Date.ToString("yyyy-MM-dd"), c.StartTime, null, c.Duration, c.TotalSpots, c.Status.ToString());
        return tweak?.Invoke(cmd) ?? cmd;
    }

    [Fact]
    public async Task Update_IgnoresItself_WhenCheckingConflicts()
    {
        var c = _f.AddClass(new DateOnly(2026, 10, 20), "10:00", "10:45", studioId: _f.Studio.Id, teacherId: _f.Teacher.Id);

        var updated = await UpdateHandler().Handle(Update(c, x => x with { StartTime = "10:15" }), default);

        updated.StartTime.Should().Be("10:15");
        updated.EndTime.Should().Be("11:00");
    }

    [Fact]
    public async Task Update_NewConflict_Returns409()
    {
        var c = _f.AddClass(new DateOnly(2026, 10, 20), "10:00", "10:45", studioId: _f.Studio.Id, teacherId: _f.Teacher.Id);
        _f.AddClass(new DateOnly(2026, 10, 20), "12:00", "12:45", studioId: _f.Studio.Id);

        var act = () => UpdateHandler().Handle(Update(c, x => x with { StartTime = "12:30" }), default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CLASS_CONFLICT");
    }

    [Fact]
    public async Task Update_PreexistingConflict_StillAllowsRenaming()
    {
        var a = _f.AddClass(new DateOnly(2026, 10, 20), "10:00", "10:45", studioId: _f.Studio.Id);
        _f.AddClass(new DateOnly(2026, 10, 20), "10:15", "11:00", studioId: _f.Studio.Id);

        var updated = await UpdateHandler().Handle(Update(a, x => x with { Name = "Novo nome" }), default);

        updated.Name.Should().Be("Novo nome");
    }

    [Fact]
    public async Task Update_SeriesOccurrence_BecomesException_OnlyWhenContentChanges()
    {
        var series = (await _f.CreateAsync()).Data.Series;
        var occ = (await _f.OccurrencesAsync(series.Id)).First();

        var same = await UpdateHandler().Handle(Update(occ), default);
        same.IsException.Should().BeFalse();

        var renamed = await UpdateHandler().Handle(Update(occ, x => x with { Name = "Especial" }), default);
        renamed.IsException.Should().BeTrue();
        renamed.SeriesId.Should().Be(series.Id);
    }
}

public class ClassListPeriodTests
{
    private readonly SeriesFixture _f = new();

    [Fact]
    public async Task List_FromTo_ReturnsOnlyThePeriod_Inclusive()
    {
        _f.AddClass(new DateOnly(2026, 9, 30), "10:00", "10:45");
        _f.AddClass(new DateOnly(2026, 10, 1), "10:00", "10:45");
        _f.AddClass(new DateOnly(2026, 10, 31), "10:00", "10:45");
        _f.AddClass(new DateOnly(2026, 11, 1), "10:00", "10:45");

        var list = await _f.Classes.ListAsync(null, null, null, default, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        list.Select(c => c.Date).Should().Equal(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
    }

    [Fact]
    public async Task List_WithoutFilters_KeepsReturningEverything()
    {
        _f.AddClass(new DateOnly(2026, 9, 30), "10:00", "10:45");
        _f.AddClass(new DateOnly(2026, 11, 1), "10:00", "10:45");

        (await _f.Classes.ListAsync(null, null, null)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ListPaged_AndTeacherList_AcceptPeriod()
    {
        _f.AddClass(new DateOnly(2026, 10, 5), "10:00", "10:45", teacherId: _f.Teacher.Id, studioId: _f.Studio.Id);
        _f.AddClass(new DateOnly(2026, 12, 5), "10:00", "10:45", teacherId: _f.Teacher.Id, studioId: _f.Studio.Id);

        var (items, total) = await _f.Classes.ListPagedAsync(null, null, null, 1, 20, default,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        total.Should().Be(1);
        items.Single().Date.Should().Be(new DateOnly(2026, 10, 5));

        var mine = await _f.Classes.ListByTeacherAsync(_f.Teacher.Id, null, default, new DateOnly(2026, 12, 1), null);
        mine.Should().ContainSingle();
    }
}
