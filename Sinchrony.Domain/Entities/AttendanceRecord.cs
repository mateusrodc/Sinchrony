using Sinchrony.Domain.Enums;

namespace Sinchrony.Domain.Entities;

public class AttendanceRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid BookingId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public BookingStatus Status { get; private set; } = BookingStatus.confirmed;
    public Guid? ConfirmedById { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public Booking? Booking { get; private set; }
    public Class? Class { get; private set; }
    public User? Student { get; private set; }
    public User? ConfirmedBy { get; private set; }

    protected AttendanceRecord() { }

    public static AttendanceRecord Create(Guid bookingId, Guid classId, Guid studentId)
        => new() { BookingId = bookingId, ClassId = classId, StudentId = studentId };

    public void Confirm(Guid confirmedById)
    {
        Status = BookingStatus.attended;
        ConfirmedById = confirmedById;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(string status, Guid? confirmedById = null)
    {
        Status = status switch
        {
            "attended" => BookingStatus.attended,
            "no_show" => BookingStatus.no_show,
            _ => BookingStatus.confirmed
        };

        if (Status == BookingStatus.attended)
        {
            ConfirmedById = confirmedById;
            ConfirmedAt = DateTime.UtcNow;
        }
    }

    public void MarkNoShow() => Status = BookingStatus.no_show;
    public void MarkConfirmed() => Status = BookingStatus.confirmed;
}