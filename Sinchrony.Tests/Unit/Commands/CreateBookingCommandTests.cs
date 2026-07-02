using FluentAssertions;
using Moq;
using Sinchrony.Application.Bookings.Commands.CreateBooking;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Xunit;

namespace Sinchrony.Tests.Unit.Commands;

public class CreateBookingCommandTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IClassRepository> _classRepo = new();
    private readonly Mock<IBookingRepository> _bookingRepo = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepo = new();
    private readonly Mock<ICreditTransactionRepository> _creditTxRepo = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CreateBookingCommandTests()
    {
        _unitOfWork.Setup(u => u.BeginTransactionAsync(default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.CommitAsync(default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.RollbackAsync(default)).Returns(Task.CompletedTask);
        _auditService.Setup(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _creditTxRepo.Setup(r => r.AddAsync(It.IsAny<CreditTransaction>(), default)).Returns(Task.CompletedTask);
        _creditTxRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
        _attendanceRepo.Setup(r => r.AddAsync(It.IsAny<AttendanceRecord>(), default)).Returns(Task.CompletedTask);
        _attendanceRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
    }

    private CreateBookingCommandHandler CreateHandler() =>
        new(_userRepo.Object, _classRepo.Object, _bookingRepo.Object,
            _attendanceRepo.Object, _creditTxRepo.Object,
            _auditService.Object, _unitOfWork.Object);

    private static User CreateStudent(int credits = 5)
    {
        var u = User.Create("Student", "student@test.com", null, "hash", Role.student);
        if (credits > 0) u.AddCredits(credits);
        return u;
    }

    private static Class CreateClass(int totalSpots = 10)
        => Class.Create("Yoga", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            "07:00", "07:45", 45, totalSpots);

    [Fact]
    public async Task Handle_ValidRequest_CreatesBookingAndDeductsCredit()
    {
        var student = CreateStudent();
        var @class = CreateClass();

        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);
        _userRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
        _classRepo.Setup(r => r.GetByIdAsync(@class.Id, default)).ReturnsAsync(@class);
        _classRepo.Setup(r => r.CountActiveBookingsWithLockAsync(@class.Id, default)).ReturnsAsync(0);
        _bookingRepo.Setup(r => r.HasActiveBookingAsync(student.Id, @class.Id, default)).ReturnsAsync(false);
        _bookingRepo.Setup(r => r.HasTimeConflictAsync(student.Id, @class.Date, @class.StartTime, @class.EndTime, @class.Id, default)).ReturnsAsync(false);
        _bookingRepo.Setup(r => r.AddAsync(It.IsAny<Booking>(), default)).Returns(Task.CompletedTask);
        _bookingRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new CreateBookingCommand(student.Id, @class.Id, null), default);

        result.Should().NotBeNull();
        result.Status.Should().Be("confirmed");
        student.Credits.Should().Be(4);
    }

    [Fact]
    public async Task Handle_InsufficientCredits_ThrowsDomainException()
    {
        var student = CreateStudent(0);
        var @class = CreateClass();

        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);
        _classRepo.Setup(r => r.GetByIdAsync(@class.Id, default)).ReturnsAsync(@class);

        var act = () => CreateHandler().Handle(new CreateBookingCommand(student.Id, @class.Id, null), default);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("INSUFFICIENT_CREDITS");
    }

    [Fact]
    public async Task Handle_ClassFull_ThrowsDomainException()
    {
        var student = CreateStudent();
        var @class = CreateClass(totalSpots: 5);

        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);
        _classRepo.Setup(r => r.GetByIdAsync(@class.Id, default)).ReturnsAsync(@class);
        _bookingRepo.Setup(r => r.HasActiveBookingAsync(student.Id, @class.Id, default)).ReturnsAsync(false);
        _bookingRepo.Setup(r => r.HasTimeConflictAsync(student.Id, @class.Date, @class.StartTime, @class.EndTime, @class.Id, default)).ReturnsAsync(false);
        _classRepo.Setup(r => r.CountActiveBookingsWithLockAsync(@class.Id, default)).ReturnsAsync(5);

        var act = () => CreateHandler().Handle(new CreateBookingCommand(student.Id, @class.Id, null), default);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("CLASS_FULL");
    }

    [Fact]
    public async Task Handle_DuplicateBooking_ThrowsDomainException()
    {
        var student = CreateStudent();
        var @class = CreateClass();

        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);
        _classRepo.Setup(r => r.GetByIdAsync(@class.Id, default)).ReturnsAsync(@class);
        _bookingRepo.Setup(r => r.HasActiveBookingAsync(student.Id, @class.Id, default)).ReturnsAsync(true);

        var act = () => CreateHandler().Handle(new CreateBookingCommand(student.Id, @class.Id, null), default);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("BOOKING_CONFLICT");
    }

    [Fact]
    public async Task Handle_TimeConflict_ThrowsDomainException()
    {
        var student = CreateStudent();
        var @class = CreateClass();

        _userRepo.Setup(r => r.GetByIdAsync(student.Id, default)).ReturnsAsync(student);
        _classRepo.Setup(r => r.GetByIdAsync(@class.Id, default)).ReturnsAsync(@class);
        _bookingRepo.Setup(r => r.HasActiveBookingAsync(student.Id, @class.Id, default)).ReturnsAsync(false);
        _bookingRepo.Setup(r => r.HasTimeConflictAsync(student.Id, @class.Date, @class.StartTime, @class.EndTime, @class.Id, default)).ReturnsAsync(true);

        var act = () => CreateHandler().Handle(new CreateBookingCommand(student.Id, @class.Id, null), default);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("TIME_CONFLICT");
    }
}