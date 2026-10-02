using FluentAssertions;
using Moq;
using Sinchrony.Application.Attendance;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// DEMANDA_PRESENCA_EM_AULA_FUTURA_BACKEND.md — guarda de horário, auditoria e sincronia Booking/Attendance.
public class AttendanceChangeServiceTests
{
    private static readonly DateOnly ClassDate = new(2026, 10, 3);

    private static Class CreateClass(string start = "22:00") =>
        Class.Create("Spinning", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ClassDate, start, "23:00", 60, 20);

    // Instante UTC equivalente a um horário de Brasília (UTC-3).
    private static DateTime Brt(int day, int hour, int minute) =>
        new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Unspecified).AddHours(3);

    [Fact]
    public void Attended_DayBefore_IsRejected()
    {
        var r = AttendanceChangeService.CheckWindow(CreateClass(), "attended", Brt(2, 5, 21), 10);
        r!.Value.Code.Should().Be("CLASS_NOT_STARTED");
    }

    [Fact]
    public void Attended_20MinutesBefore_IsAccepted_ButNoShowIsTooEarly()
    {
        var c = CreateClass();
        AttendanceChangeService.CheckWindow(c, "attended", Brt(3, 21, 40), 10).Should().BeNull();
        AttendanceChangeService.CheckWindow(c, "no_show", Brt(3, 21, 40), 10)!.Value.Code
            .Should().Be("NO_SHOW_TOO_EARLY");
    }

    [Fact]
    public void Attended_ChecksBrasiliaTime_NotUtc()
    {
        var c = CreateClass();
        // 20:00 BRT = 23:00 UTC: comparar direto com UTC aceitaria por engano.
        AttendanceChangeService.CheckWindow(c, "attended", Brt(3, 20, 0), 10)!.Value.Code
            .Should().Be("CLASS_NOT_STARTED");
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(11, true)]
    public void NoShow_RespectsTolerance(int minutesAfterStart, bool accepted)
    {
        var c = CreateClass("22:00");
        var r = AttendanceChangeService.CheckWindow(c, "no_show", Brt(3, 22, minutesAfterStart), 10);
        (r is null).Should().Be(accepted);
    }

    [Fact]
    public void Pending_IsAlwaysAccepted()
    {
        AttendanceChangeService.CheckWindow(CreateClass(), "confirmed", Brt(1, 0, 0), 10).Should().BeNull();
    }

    [Fact]
    public void CancelledClass_RejectsAttendedAndNoShow()
    {
        var c = CreateClass();
        c.Cancel();
        AttendanceChangeService.CheckWindow(c, "attended", Brt(3, 22, 30), 10)!.Value.Code.Should().Be("CLASS_CANCELLED");
        AttendanceChangeService.CheckWindow(c, "no_show", Brt(3, 23, 0), 10)!.Value.Code.Should().Be("CLASS_CANCELLED");
    }

    [Fact]
    public void Normalize_MapsPendingAndRejectsUnknown()
    {
        AttendanceChangeService.Normalize("pending").Should().Be("confirmed");
        AttendanceChangeService.Normalize("confirmed").Should().Be("confirmed");
        var act = () => AttendanceChangeService.Normalize("bogus");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_STATUS");
    }

    private class Fixture
    {
        public Mock<IAttendanceRepository> Attendance = new();
        public Mock<IBookingRepository> Bookings = new();
        public Mock<ISettingsRepository> Settings = new();
        public Mock<INoShowPenaltyService> Penalty = new();
        public Mock<IWaitlistPromotionService> Waitlist = new();
        public Mock<IAuditService> Audit = new();

        public Fixture() =>
            Penalty.Setup(p => p.ReverseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        public AttendanceChangeService Service => new(
            Attendance.Object, Bookings.Object, Settings.Object,
            Penalty.Object, Waitlist.Object, Audit.Object);
    }

    // Aula já terminada (data no passado), então a guarda de horário não interfere.
    private static Class PastClass() =>
        Class.Create("Spinning", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 1, 10), "07:00", "08:00", 60, 20);

    [Fact]
    public async Task NoShowThenPending_RestoresBothSides_ReversesPenalty_AndAudits()
    {
        var f = new Fixture();
        var c = PastClass();
        var booking = Booking.Create(c.Id, Guid.NewGuid(), null);
        var record = AttendanceRecord.Create(booking.Id, c.Id, booking.StudentId);
        f.Attendance.Setup(a => a.GetByBookingAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        await f.Service.ApplyAsync(c, [new AttendanceChangeItem(booking, "no_show")], Guid.NewGuid(), "single", default);
        booking.Status.Should().Be(BookingStatus.no_show);
        record.Status.Should().Be(BookingStatus.no_show);
        f.Penalty.Verify(p => p.ApplyAsync(booking.StudentId, It.IsAny<CancellationToken>()), Times.Once);
        f.Waitlist.Verify(w => w.PromoteNextAsync(c.Id, c.Name, It.IsAny<CancellationToken>()), Times.Once);

        var result = await f.Service.ApplyAsync(c, [new AttendanceChangeItem(booking, "pending")], Guid.NewGuid(), "single", default);
        booking.Status.Should().Be(BookingStatus.confirmed);
        booking.CheckedIn.Should().BeFalse();
        record.Status.Should().Be(BookingStatus.confirmed);
        f.Penalty.Verify(p => p.ReverseAsync(booking.StudentId, It.IsAny<CancellationToken>()), Times.Once);
        result.Warnings.Should().Contain(AttendanceChangeService.WaitlistNotUndoneWarning);

        f.Audit.Verify(a => a.LogAsync("attendance.updated", "AttendanceRecord", record.Id, It.IsAny<Guid?>(),
            It.Is<string>(d => d.Contains("Previous: confirmed") && d.Contains("New: no_show")),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Audit.Verify(a => a.LogAsync("attendance.updated", "AttendanceRecord", record.Id, It.IsAny<Guid?>(),
            It.Is<string>(d => d.Contains("Previous: no_show") && d.Contains("New: confirmed")),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SameStatusAgain_WritesNoAudit()
    {
        var f = new Fixture();
        var c = PastClass();
        var booking = Booking.Create(c.Id, Guid.NewGuid(), null);
        var record = AttendanceRecord.Create(booking.Id, c.Id, booking.StudentId);
        f.Attendance.Setup(a => a.GetByBookingAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        await f.Service.ApplyAsync(c, [new AttendanceChangeItem(booking, "attended")], null, "single", default);
        await f.Service.ApplyAsync(c, [new AttendanceChangeItem(booking, "attended")], null, "single", default);

        f.Audit.Verify(a => a.LogAsync("attendance.updated", It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        booking.CheckedIn.Should().BeTrue();
    }

    [Fact]
    public async Task FutureClass_RejectsWholeBatch_AuditsRejection_AndChangesNothing()
    {
        var f = new Fixture();
        var c = Class.Create("Spinning", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2999, 1, 1), "07:00", "08:00", 60, 20);
        var b1 = Booking.Create(c.Id, Guid.NewGuid(), null);
        var b2 = Booking.Create(c.Id, Guid.NewGuid(), null);

        var act = () => f.Service.ApplyAsync(c,
            [new AttendanceChangeItem(b1, "pending"), new AttendanceChangeItem(b2, "attended")],
            Guid.NewGuid(), "bulk", default);

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("CLASS_NOT_STARTED");
        b1.Status.Should().Be(BookingStatus.confirmed);
        b2.CheckedIn.Should().BeFalse();
        f.Attendance.Verify(a => a.AddAsync(It.IsAny<AttendanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Audit.Verify(a => a.LogAsync("attendance.rejected", "Class", c.Id, It.IsAny<Guid?>(),
            It.Is<string>(d => d.Contains("CLASS_NOT_STARTED")),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
