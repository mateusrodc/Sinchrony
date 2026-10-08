using Microsoft.EntityFrameworkCore;
using Moq;
using Sinchrony.Application.Classes;
using Sinchrony.Application.Series;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;

namespace Sinchrony.Tests.Unit.Series;

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

// Banco em memória + repositórios reais; só unidade de trabalho, auditoria e unidade do admin são mock.
public sealed class SeriesFixture
{
    // 05/10/2026 12:00 UTC = 09:00 em Brasília -> "hoje" é 2026-10-05 (segunda-feira).
    public static readonly DateOnly Today = new(2026, 10, 5);

    public ApplicationDbContext Db { get; }
    public ClassRepository Classes { get; }
    public ClassSeriesRepository SeriesRepo { get; }
    public Mock<IUnitContext> UnitContext { get; } = new();
    public Mock<IAuditService> Audit { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public ClassPlanning Planning { get; }
    public ClassSeriesService Service { get; }

    public User Teacher { get; } = User.Create("Prof", "p@test.com", null, "h", Role.teacher);
    public User OtherTeacher { get; } = User.Create("Outro", "o@test.com", null, "h", Role.teacher);
    public ClassType Type { get; } = ClassType.Create("Pilates");
    public Studio Studio { get; } = Studio.Create("Sala 1", "Rua", 10);
    public Studio OtherStudio { get; } = Studio.Create("Sala 2", "Rua", 10);
    public Guid AdminId { get; } = Guid.NewGuid();

    public SeriesFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Db = new ApplicationDbContext(options);
        Classes = new ClassRepository(Db);
        SeriesRepo = new ClassSeriesRepository(Db);

        Db.AddRange(Teacher, OtherTeacher, Type, Studio, OtherStudio);
        Db.SaveChanges();

        UnitContext.SetupGet(u => u.IsGlobalAdmin).Returns(true);
        Audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWork.Setup(u => u.AcquireAdvisoryLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Planning = NewPlanning();
        Service = new ClassSeriesService(Planning, SeriesRepo, Classes, UnitOfWork.Object, Audit.Object);
    }

    public ClassPlanning NewPlanning()
        => new(new StudioRepository(Db), new ClassTypeRepository(Db), new UserRepository(Db), Classes,
            UnitContext.Object, new FixedClock(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)));

    // Segunda/quarta/sexta 14:45 (45 min), de 06/10 a 30/11/2026 = 24 aulas.
    public ClassSeriesInput Input(
        string startDate = "2026-10-06", string endDate = "2026-11-30", int[]? days = null,
        string startTime = "14:45", int duration = 45, string[]? excluded = null,
        Guid? teacherId = null, Guid? studioId = null)
        => new("Pilates", Type.Id, teacherId ?? Teacher.Id, studioId ?? Studio.Id, startTime, duration, 20,
            days ?? [1, 3, 5], startDate, endDate, excluded);

    public Task<SeriesCreateResult> CreateAsync(ClassSeriesInput? input = null, Guid? requestId = null)
        => Service.CreateAsync(input ?? Input(), requestId ?? Guid.NewGuid(), AdminId, default);

    public async Task<List<Class>> OccurrencesAsync(Guid seriesId)
        => await Db.Classes.Where(c => c.SeriesId == seriesId).OrderBy(c => c.Date).ToListAsync();

    public Class AddClass(DateOnly date, string start, string end, Guid? teacherId = null, Guid? studioId = null,
        ClassStatus status = ClassStatus.scheduled)
    {
        var c = Class.Create("Avulsa", Type.Id, teacherId ?? OtherTeacher.Id, studioId ?? OtherStudio.Id,
            date, start, end, 45, 10);
        typeof(Class).GetProperty(nameof(Class.Status))!.SetValue(c, status);
        Db.Classes.Add(c);
        Db.SaveChanges();
        return c;
    }

    public Booking Book(Class c)
    {
        var student = User.Create("S", $"{Guid.NewGuid()}@t.com", null, "h", Role.student);
        Db.Users.Add(student);
        var booking = Booking.Create(c.Id, student.Id, null);
        Db.Bookings.Add(booking);
        Db.SaveChanges();
        return booking;
    }
}
