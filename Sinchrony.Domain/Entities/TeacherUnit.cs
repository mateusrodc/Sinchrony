namespace Sinchrony.Domain.Entities;

public class TeacherUnit
{
    public Guid TeacherId { get; private set; }
    public Guid UnitId { get; private set; }

    public User? Teacher { get; private set; }
    public Unit? Unit { get; private set; }

    protected TeacherUnit() { }

    public static TeacherUnit Create(Guid teacherId, Guid unitId)
        => new() { TeacherId = teacherId, UnitId = unitId };
}