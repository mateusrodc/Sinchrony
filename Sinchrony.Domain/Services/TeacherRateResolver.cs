using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Services;

// Resolve qual valor de aula / bônus vale numa data. Lógica pura (sem banco) para ser testável:
// o relatório carrega todas as linhas de vigência uma vez e resolve aula por aula em memória.
public class TeacherRateResolver
{
    private readonly IReadOnlyList<ClassRate> _classRates;
    private readonly IReadOnlyList<TeacherBonusRate> _bonusRates;

    public TeacherRateResolver(IEnumerable<ClassRate> classRates, IEnumerable<TeacherBonusRate> bonusRates)
    {
        _classRates = classRates.ToList();
        _bonusRates = bonusRates.ToList();
    }

    // Linha do próprio ClassTypeId com o maior EffectiveFrom <= date; se não houver, a linha
    // padrão (ClassTypeId = null) pela mesma regra; sem nenhuma, 0.
    public decimal ResolveClassValue(Guid classTypeId, DateOnly date)
    {
        var own = Latest(_classRates.Where(r => r.ClassTypeId == classTypeId), date);
        if (own is not null) return own.Value;

        var fallback = Latest(_classRates.Where(r => r.ClassTypeId is null), date);
        return fallback?.Value ?? 0m;
    }

    public decimal ResolveBonusPerStudent(DateOnly date)
        => _bonusRates
            .Where(r => r.EffectiveFrom <= date)
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault()?.ValuePerStudent ?? 0m;

    private static ClassRate? Latest(IEnumerable<ClassRate> rates, DateOnly date)
        => rates
            .Where(r => r.EffectiveFrom <= date)
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault();
}
