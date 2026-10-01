using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

// Família de um aluno para a regra de compra única: o titular (RootId) e todo mundo ligado a ele.
public record StudentFamily(Guid RootId, IReadOnlyCollection<Guid> UserIds);

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<User>> ListStudentsAsync(string? status, CancellationToken ct = default);
    // includeAdmins=false (padrão) preserva o comportamento de sempre: só Role.teacher — é o que
    // alimenta o seletor de professor na tela de aula, e isso não muda. includeAdmins=true é o
    // opt-in novo usado pela tela "Cadastro de Usuários" (professor + admin/secretária).
    Task<IEnumerable<User>> ListTeachersAsync(string? active, bool includeAdmins = false, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<(IEnumerable<User> Items, int Total)> ListStudentsPagedAsync(
    string? status, int page, int pageSize, CancellationToken ct = default,
    string? paymentStatus = null);

    Task<User?> GetByCpfAsync(string cpf, CancellationToken ct = default);

    Task<IEnumerable<User>> ListStudentsByUnitAsync(Guid unitId, CancellationToken ct = default);
    Task<IEnumerable<User>> ListTeachersByUnitAsync(Guid unitId, CancellationToken ct = default);

    // Destinatários do alerta de assinatura vencida (studio-wide, sem escopo de unidade).
    Task<IEnumerable<User>> ListAdminsAsync(CancellationToken ct = default);

    // Família do aluno: ele mesmo, o responsável dele e os dependentes dele. Considera os DOIS
    // modelos de dependente: a tabela dependents (ResponsibleStudentId + UserId) e
    // users.IsDependent + users.ResponsibleStudentId.
    Task<StudentFamily> GetFamilyAsync(Guid userId, CancellationToken ct = default);

    // Alunos que fazem aniversário no mês (1..12), ordenados pelo dia. unitId restringe à unidade.
    Task<IReadOnlyList<User>> ListBirthdaysAsync(int month, Guid? unitId, CancellationToken ct = default);

    // Batch por id — usado pra resolver nome de aluno sem N+1 (ex: GET /api/alerts).
    Task<IEnumerable<User>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}