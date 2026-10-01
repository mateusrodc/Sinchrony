using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Services;

// DEMANDA_BACKEND_DEMANDAS_PENDENTES item 5 — critério 10: filtros do relatório de aulas do professor.
public class TeacherClassReportRepositoryTests
{
    private readonly ApplicationDbContext _db;
    private readonly ClassRepository _repo;
    private readonly User _teacher = User.Create("Prof", "p@test.com", null, "h", Role.teacher);
    private readonly ClassType _type = ClassType.Create("Spinning");
    private readonly Studio _studio = Studio.Create("Sala 1", "Rua", 10);
    private readonly Class _completed4;
    private readonly Class _completed0;
    private readonly Class _cancelled;

    public TeacherClassReportRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new ApplicationDbContext(options);
        _repo = new ClassRepository(_db);

        _db.AddRange(_teacher, _type, _studio);

        _completed4 = NewClass("A", new DateOnly(2026, 8, 3), ClassStatus.completed);
        _completed0 = NewClass("B", new DateOnly(2026, 8, 4), ClassStatus.completed);
        _cancelled = NewClass("C", new DateOnly(2026, 9, 1), ClassStatus.cancelled);
        _db.Classes.AddRange(_completed4, _completed0, _cancelled);
        _db.SaveChanges();

        // A: 4 presentes, 1 falta, 1 reserva cancelada, 1 lista de espera
        for (var i = 0; i < 4; i++) Attend(_completed4, "attended");
        Attend(_completed4, "no_show");
        Cancelled(_completed4);
        Waitlisted(_completed4);
        // B: 1 reserva só com falta -> zero presentes
        Attend(_completed0, "no_show");
        _db.SaveChanges();
    }

    private Class NewClass(string name, DateOnly date, ClassStatus status)
    {
        var c = Class.Create(name, _type.Id, _teacher.Id, _studio.Id, date, "07:00", "08:00", 60, 10);
        typeof(Class).GetProperty(nameof(Class.Status))!.SetValue(c, status);
        return c;
    }

    private User NewStudent()
    {
        var student = User.Create("S", $"{Guid.NewGuid()}@t.com", null, "h", Role.student);
        _db.Users.Add(student);
        return student;
    }

    private void Attend(Class c, string status)
    {
        var student = NewStudent();
        var booking = Booking.Create(c.Id, student.Id, null);
        _db.Bookings.Add(booking);
        var record = AttendanceRecord.Create(booking.Id, c.Id, student.Id);
        record.UpdateStatus(status, _teacher.Id);
        _db.AttendanceRecords.Add(record);
    }

    private void Cancelled(Class c)
    {
        var b = Booking.Create(c.Id, NewStudent().Id, null);
        b.Cancel();
        _db.Bookings.Add(b);
    }

    private void Waitlisted(Class c)
    {
        var b = Booking.Create(c.Id, NewStudent().Id, null);
        typeof(Booking).GetProperty(nameof(Booking.Status))!.SetValue(b, BookingStatus.waitlisted);
        _db.Bookings.Add(b);
    }

    private static TeacherClassReportFilter Filter(
        IReadOnlyCollection<ClassStatus>? statuses = null, int? minAttended = null,
        DateOnly? from = null, DateOnly? to = null)
        => new(from, to, null, null, null, statuses, minAttended, null);

    [Fact]
    public async Task NoStatusFilter_ReturnsEveryClass_WithCounts()
    {
        var rows = await _repo.ListForTeacherReportAsync(Filter());

        rows.Should().HaveCount(3);
        var a = rows.Single(r => r.ClassName == "A");
        a.Attended.Should().Be(4);
        a.NoShow.Should().Be(1);
        a.CancelledBookings.Should().Be(1);
        a.Booked.Should().Be(5); // 4 presentes + 1 falta; cancelada e lista de espera ficam de fora
        a.Teacher.Should().Be("Prof");
        a.ClassType.Should().Be("Spinning");
    }

    [Fact]
    public async Task StatusFilter_Cancelled_ReturnsOnlyCancelled()
    {
        var rows = await _repo.ListForTeacherReportAsync(Filter([ClassStatus.cancelled]));

        rows.Should().ContainSingle().Which.ClassName.Should().Be("C");
    }

    [Fact]
    public async Task MinAttended1_HidesClassesWithoutAttendees()
    {
        var rows = await _repo.ListForTeacherReportAsync(Filter(minAttended: 1));

        rows.Should().ContainSingle().Which.ClassName.Should().Be("A");
    }

    [Fact]
    public async Task DateRange_FiltersByClassDate_AugustVsSeptember()
    {
        var august = await _repo.ListForTeacherReportAsync(
            Filter(from: new DateOnly(2026, 8, 1), to: new DateOnly(2026, 8, 31)));
        var september = await _repo.ListForTeacherReportAsync(
            Filter(from: new DateOnly(2026, 9, 1), to: new DateOnly(2026, 9, 30)));

        august.Select(r => r.ClassName).Should().BeEquivalentTo("A", "B");
        september.Select(r => r.ClassName).Should().BeEquivalentTo("C");
    }
}
