namespace Sinchrony.Domain.Entities;

// Valor pago ao professor por aula, com histórico por data de vigência. ClassTypeId nulo = valor
// padrão (vale para toda modalidade sem linha própria). Uma mudança de preço é sempre um registro
// NOVO com EffectiveFrom — nunca uma edição do antigo — para que o relatório de um mês passado
// continue saindo com os valores que valiam naquele mês.
public class ClassRate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? ClassTypeId { get; private set; }
    public decimal Value { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; private set; }

    public ClassType? ClassType { get; private set; }

    protected ClassRate() { }

    public static ClassRate Create(Guid? classTypeId, decimal value, DateOnly effectiveFrom, Guid? createdById)
        => new()
        {
            ClassTypeId = classTypeId,
            Value = value,
            EffectiveFrom = effectiveFrom,
            CreatedById = createdById
        };
}
