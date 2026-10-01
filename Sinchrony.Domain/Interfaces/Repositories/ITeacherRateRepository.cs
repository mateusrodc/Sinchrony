using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

// Histórico de valores de aula e bônus do professor (ver ClassRate / TeacherBonusRate).
public interface ITeacherRateRepository
{
    Task<IReadOnlyList<ClassRate>> ListClassRatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TeacherBonusRate>> ListBonusRatesAsync(CancellationToken ct = default);
    Task AddClassRateAsync(ClassRate rate, CancellationToken ct = default);
    Task AddBonusRateAsync(TeacherBonusRate rate, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
