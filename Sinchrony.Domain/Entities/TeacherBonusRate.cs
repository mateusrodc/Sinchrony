namespace Sinchrony.Domain.Entities;

// Bônus pago ao professor por aluno presente, com histórico por data de vigência (mesma lógica
// de ClassRate: mudança de valor = registro novo com EffectiveFrom).
public class TeacherBonusRate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public decimal ValuePerStudent { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; private set; }

    protected TeacherBonusRate() { }

    public static TeacherBonusRate Create(decimal valuePerStudent, DateOnly effectiveFrom, Guid? createdById)
        => new()
        {
            ValuePerStudent = valuePerStudent,
            EffectiveFrom = effectiveFrom,
            CreatedById = createdById
        };
}
