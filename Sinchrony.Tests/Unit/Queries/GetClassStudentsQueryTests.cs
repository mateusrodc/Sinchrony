using FluentAssertions;
using Moq;
using Sinchrony.Application.Classes.Queries.GetClassStudents;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Queries;

// DEMANDA_CLASSES_STUDENTS_ATTENDANCE_DESSINCRONIZADO_BACKEND.md (22/09/2026): o professor
// marcava presença (UpdateAttendanceCommand/ConfirmAllAttendanceCommand escrevem só em
// AttendanceRecord.Status), mas GET /classes/{id}/students lia Booking.Status — que nenhum dos
// dois comandos toca — e voltava a mostrar "pending" ao reabrir o app.
public class GetClassStudentsQueryTests
{
    private readonly Mock<IBookingRepository> _bookingRepo = new();
    private readonly Mock<IClassRepository> _classRepo = new();
    private readonly Mock<IAttendanceRepository> _attendanceRepo = new();

    private GetClassStudentsQueryHandler CreateHandler() =>
        new(_bookingRepo.Object, _classRepo.Object, _attendanceRepo.Object);

    private static Booking BookingWithStudent(Guid classId, User student)
    {
        var booking = Booking.Create(classId, student.Id, bikeNumber: null);
        typeof(Booking).GetProperty(nameof(Booking.Student))!.SetValue(booking, student);
        return booking;
    }

    [Fact]
    public async Task Handle_AttendanceMarked_ReflectsAttendanceStatus_NotStaleBookingStatus()
    {
        var classId = Guid.NewGuid();
        var student = User.Create("Student", "student@test.com", null, "hash", Role.student);
        var booking = BookingWithStudent(classId, student); // Status fica "confirmed" pra sempre

        var attendance = AttendanceRecord.Create(booking.Id, classId, student.Id);
        attendance.UpdateStatus("attended", Guid.NewGuid());

        _classRepo.Setup(r => r.GetByIdAsync(classId, default))
            .ReturnsAsync(Class.Create("Spin", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow), "10:00", "11:00", 60, 10));
        _bookingRepo.Setup(r => r.ListErpAsync(classId, null, null, default))
            .ReturnsAsync([booking]);
        _attendanceRepo.Setup(r => r.ListByClassAsync(classId, default))
            .ReturnsAsync([attendance]);

        var result = (await CreateHandler().Handle(new GetClassStudentsQuery(classId), default)).ToList();

        result.Should().ContainSingle().Which.Status.Should().Be("attended");
    }

    [Fact]
    public async Task Handle_NoAttendanceRecordYet_FallsBackToBookingStatus()
    {
        var classId = Guid.NewGuid();
        var student = User.Create("Student", "student@test.com", null, "hash", Role.student);
        var booking = BookingWithStudent(classId, student);

        _classRepo.Setup(r => r.GetByIdAsync(classId, default))
            .ReturnsAsync(Class.Create("Spin", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow), "10:00", "11:00", 60, 10));
        _bookingRepo.Setup(r => r.ListErpAsync(classId, null, null, default))
            .ReturnsAsync([booking]);
        _attendanceRepo.Setup(r => r.ListByClassAsync(classId, default))
            .ReturnsAsync([]);

        var result = (await CreateHandler().Handle(new GetClassStudentsQuery(classId), default)).ToList();

        result.Should().ContainSingle().Which.Status.Should().Be("confirmed");
    }

    [Fact]
    public async Task Handle_CancelledBooking_IsExcludedEvenWithAttendanceRecord()
    {
        var classId = Guid.NewGuid();
        var student = User.Create("Student", "student@test.com", null, "hash", Role.student);
        var booking = BookingWithStudent(classId, student);
        booking.Cancel();

        var attendance = AttendanceRecord.Create(booking.Id, classId, student.Id);
        attendance.UpdateStatus("attended", Guid.NewGuid());

        _classRepo.Setup(r => r.GetByIdAsync(classId, default))
            .ReturnsAsync(Class.Create("Spin", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                DateOnly.FromDateTime(DateTime.UtcNow), "10:00", "11:00", 60, 10));
        _bookingRepo.Setup(r => r.ListErpAsync(classId, null, null, default))
            .ReturnsAsync([booking]);
        _attendanceRepo.Setup(r => r.ListByClassAsync(classId, default))
            .ReturnsAsync([attendance]);

        var result = await CreateHandler().Handle(new GetClassStudentsQuery(classId), default);

        result.Should().BeEmpty();
    }
}
