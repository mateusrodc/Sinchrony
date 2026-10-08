using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Sinchrony.Application.Classes;
using Sinchrony.Application.Classes.Commands.ActivateClass;
using Sinchrony.Application.Classes.Commands.CreateClass;
using Sinchrony.Application.Series;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Xunit;

namespace Sinchrony.Tests.Unit.Series;

// DEMANDA_AULAS_RECORRENTES_AJUSTES_BACKEND.md — sala não bloqueia, professor sim.
public class StudioOverlapTests
{
    private readonly SeriesFixture _f = new();

    private CreateClassCommandHandler CreateHandler() => new(_f.Planning, _f.Classes, _f.Audit.Object);

    private CreateClassCommand Create(Guid teacherId)
        => new(_f.AdminId, "Jump", _f.Type.Id, teacherId, _f.Studio.Id, "2026-10-20", "10:00", null, 60, 15);

    [Fact]
    public async Task SingleClass_SameStudioAndTime_DifferentTeacher_IsAccepted()
    {
        _f.AddClass(new DateOnly(2026, 10, 20), "10:00", "11:00", teacherId: _f.OtherTeacher.Id, studioId: _f.Studio.Id);

        var created = await CreateHandler().Handle(Create(_f.Teacher.Id), default);

        created.StudioId.Should().Be(_f.Studio.Id);
        (await _f.Db.Classes.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SingleClass_SameTeacherSameTime_Returns409WithTeacherType()
    {
        _f.AddClass(new DateOnly(2026, 10, 20), "10:30", "11:30", teacherId: _f.Teacher.Id, studioId: _f.OtherStudio.Id);

        var act = () => CreateHandler().Handle(Create(_f.Teacher.Id), default);

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        ex.Code.Should().Be("CLASS_CONFLICT");
        ((IEnumerable<ClassConflictDto>)ex.Extensions!["conflicts"]!).Should().OnlyContain(c => c.Type == "teacher");
    }

    [Fact]
    public async Task SingleClass_SameStudioAndTeacher_ReportsOnlyTeacherAsBlocking()
    {
        _f.AddClass(new DateOnly(2026, 10, 20), "10:00", "11:00", teacherId: _f.Teacher.Id, studioId: _f.Studio.Id);

        var act = () => CreateHandler().Handle(Create(_f.Teacher.Id), default);

        var ex = (await act.Should().ThrowAsync<DomainException>()).Which;
        ((IEnumerable<ClassConflictDto>)ex.Extensions!["conflicts"]!).Should().ContainSingle().Which.Type.Should().Be("teacher");
    }

    [Fact]
    public async Task Series_OnlyStudioOverlap_IsCreated_AndPreviewHasWarningsNotConflicts()
    {
        // outro professor ocupa a mesma sala nas sextas de outubro, no mesmo horário
        foreach (var d in new[] { 9, 16, 23, 30 })
            _f.AddClass(new DateOnly(2026, 10, d), "14:45", "15:30", teacherId: _f.OtherTeacher.Id, studioId: _f.Studio.Id);

        var preview = await _f.Service.PreviewAsync(_f.Input(), default);
        preview.ConflictsCount.Should().Be(0);
        preview.WarningsCount.Should().Be(4);
        preview.Occurrences.Where(o => o.Conflicts.Count > 0).Should()
            .OnlyContain(o => o.Conflicts.All(c => c.Type == "studio"));

        var result = await _f.CreateAsync();
        result.Data.Created.Should().Be(24);
    }

    [Fact]
    public async Task Series_TeacherConflict_Blocks_UntilDateIsExcluded()
    {
        _f.AddClass(new DateOnly(2026, 10, 9), "14:45", "15:30", teacherId: _f.Teacher.Id, studioId: _f.OtherStudio.Id);

        var preview = await _f.Service.PreviewAsync(_f.Input(), default);
        preview.ConflictsCount.Should().Be(1);
        preview.WarningsCount.Should().Be(0);

        var act = () => _f.CreateAsync();
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SERIES_HAS_CONFLICTS");

        (await _f.CreateAsync(_f.Input(excluded: ["2026-10-09"]))).Data.Created.Should().Be(23);
    }

    [Fact]
    public async Task GroupUpdate_StudioOverlapApplies_ButTeacherConflictIsSkipped()
    {
        var series = (await _f.CreateAsync(_f.Input(startDate: "2026-10-08", endDate: "2026-10-31"))).Data.Series;
        // 12/10: terceiro professor na Sala 2 (só sobreposição de sala); 14/10: OtherTeacher já ocupado
        var third = Sinchrony.Domain.Entities.User.Create("Terceiro", "t@test.com", null, "h", Role.teacher);
        _f.Db.Users.Add(third);
        _f.Db.SaveChanges();
        _f.AddClass(new DateOnly(2026, 10, 12), "14:45", "15:30", teacherId: third.Id, studioId: _f.OtherStudio.Id);
        _f.AddClass(new DateOnly(2026, 10, 14), "14:45", "15:30", teacherId: _f.OtherTeacher.Id, studioId: _f.OtherStudio.Id);

        var room = await _f.Service.UpdateOccurrencesAsync(series.Id, new DateOnly(2026, 10, 8),
            new ClassSeriesChanges(null, null, null, _f.OtherStudio.Id, null, null, null), _f.AdminId, default);
        room.Skipped.Should().NotContain(s => s.Reason == SkipReason.Conflict);

        var teacher = await _f.Service.UpdateOccurrencesAsync(series.Id, new DateOnly(2026, 10, 8),
            new ClassSeriesChanges(null, null, _f.OtherTeacher.Id, null, null, null, null), _f.AdminId, default);
        teacher.Skipped.Should().ContainSingle(s => s.Date == "2026-10-14" && s.Reason == SkipReason.Conflict);
    }

    [Fact]
    public async Task Extend_StudioOverlapAccepted_TeacherConflictReturns409()
    {
        var series = (await _f.CreateAsync(_f.Input(startDate: "2026-10-08", endDate: "2026-10-31"))).Data.Series;
        _f.AddClass(new DateOnly(2026, 11, 2), "14:45", "15:30", teacherId: _f.OtherTeacher.Id, studioId: _f.Studio.Id);
        _f.AddClass(new DateOnly(2026, 11, 4), "14:45", "15:30", teacherId: _f.Teacher.Id, studioId: _f.OtherStudio.Id);

        var dry = await _f.Service.ExtendAsync(series.Id, "2026-11-06", null, true, _f.AdminId, default);
        dry.Preview!.ConflictsCount.Should().Be(1);
        dry.Preview.WarningsCount.Should().Be(1);

        var act = () => _f.Service.ExtendAsync(series.Id, "2026-11-06", null, false, _f.AdminId, default);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("SERIES_HAS_CONFLICTS");

        var ok = await _f.Service.ExtendAsync(series.Id, "2026-11-06", ["2026-11-04"], false, _f.AdminId, default);
        ok.Created!.Created.Should().Be(2);
    }

    [Fact]
    public async Task Activate_TeacherConflictReturns409_StudioOverlapReactivates()
    {
        var handler = new ActivateClassCommandHandler(_f.Planning, _f.Classes, _f.Audit.Object);
        var cancelled = _f.AddClass(new DateOnly(2026, 10, 20), "10:00", "11:00",
            teacherId: _f.Teacher.Id, studioId: _f.Studio.Id, status: ClassStatus.cancelled);
        var other = _f.AddClass(new DateOnly(2026, 10, 20), "10:30", "11:30",
            teacherId: _f.Teacher.Id, studioId: _f.OtherStudio.Id);

        var act = () => handler.Handle(new ActivateClassCommand(_f.AdminId, cancelled.Id), default);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CLASS_CONFLICT");
        _f.Db.ChangeTracker.Clear();
        (await _f.Db.Classes.AsNoTracking().SingleAsync(c => c.Id == cancelled.Id)).Status.Should().Be(ClassStatus.cancelled);

        // a outra aula passa para outro professor na mesma sala: sobra só sobreposição de sala
        var o = await _f.Db.Classes.SingleAsync(c => c.Id == other.Id);
        o.Update(o.Name, o.ClassTypeId, _f.OtherTeacher.Id, _f.Studio.Id, o.Date, o.StartTime, o.EndTime,
            o.Duration, o.TotalSpots, o.Status);
        await _f.Db.SaveChangesAsync();
        _f.Db.ChangeTracker.Clear();

        var activated = await handler.Handle(new ActivateClassCommand(_f.AdminId, cancelled.Id), default);
        activated.Status.Should().Be(ClassStatus.scheduled);
    }

    [Fact]
    public async Task Extend_TakesSeriesAndTeacherLocksInsideATransaction()
    {
        var series = (await _f.CreateAsync(_f.Input(startDate: "2026-10-08", endDate: "2026-10-31"))).Data.Series;
        _f.UnitOfWork.Invocations.Clear();

        await _f.Service.ExtendAsync(series.Id, "2026-11-06", null, false, _f.AdminId, default);

        _f.UnitOfWork.Verify(u => u.AcquireAdvisoryLockAsync($"class-series-extend:{series.Id}", It.IsAny<CancellationToken>()), Times.Once);
        _f.UnitOfWork.Verify(u => u.AcquireAdvisoryLockAsync(ClassPlanning.TeacherLockKey(_f.Teacher.Id), It.IsAny<CancellationToken>()), Times.Once);
        _f.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_TakesTeacherLock()
    {
        await CreateHandler().Handle(Create(_f.Teacher.Id), default);

        _f.UnitOfWork.Verify(u => u.AcquireAdvisoryLockAsync(ClassPlanning.TeacherLockKey(_f.Teacher.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Extend_SecondCallWithSameEnd_GetsCleanValidationError()
    {
        var series = (await _f.CreateAsync(_f.Input(startDate: "2026-10-08", endDate: "2026-10-31"))).Data.Series;
        await _f.Service.ExtendAsync(series.Id, "2026-11-06", null, false, _f.AdminId, default);

        var act = () => _f.Service.ExtendAsync(series.Id, "2026-11-06", null, false, _f.AdminId, default);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _f.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
