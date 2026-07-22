using Sinchrony.Domain.Enums;

namespace Sinchrony.Domain.Entities;

public class Booking
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public BookingStatus Status { get; private set; } = BookingStatus.confirmed;
    public int? BikeNumber { get; private set; }
    public bool CheckedIn { get; private set; }
    public DateTime BookedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public Guid? DependentId { get; private set; }

    public Class? Class { get; private set; }
    public User? Student { get; private set; }
    public AttendanceRecord? AttendanceRecord { get; private set; }

    protected Booking() { }

    public static Booking Create(Guid classId, Guid studentId, int? bikeNumber, Guid? dependentId = null)
    {
        return new Booking
        {
            ClassId = classId,
            StudentId = studentId,
            BikeNumber = bikeNumber,
            DependentId = dependentId,
            Status = BookingStatus.confirmed,
            BookedAt = DateTime.UtcNow
        };
    }

    public void Cancel() { Status = BookingStatus.cancelled; UpdatedAt = DateTime.UtcNow; }
    public void MarkAttended() { Status = BookingStatus.attended; CheckedIn = true; UpdatedAt = DateTime.UtcNow; }
    public void MarkNoShow() { Status = BookingStatus.no_show; UpdatedAt = DateTime.UtcNow; }
    public void SetCheckedIn(bool value)
    {
        CheckedIn = value;
    }
    public void Reschedule(Guid newClassId)
    {
        ClassId = newClassId;
    }
}