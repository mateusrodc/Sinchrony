using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

// Filtros da listagem de assinaturas do ERP. UnitId restringe às assinaturas de alunos daquela
// unidade. Status filtra por SubscriptionPaymentStatus (up_to_date|retrying|overdue|cancelled).
public record SubscriptionListFilter(
    string? Status = null,
    string? StudentSearch = null,
    Guid? UnitId = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null);

public record SubscriptionSummary(
    int UpToDate, int Retrying, int Overdue, int Cancelled,
    decimal ExpectedMonthlyRevenue, decimal OverdueAmount);

public record SubscriptionListPage(
    IReadOnlyList<StudentPackage> Items, int Total, SubscriptionSummary Summary);

public interface IStudentPackageRepository
{
    Task<StudentPackage?> GetActiveByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<StudentPackage?> GetQueuedByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<IEnumerable<StudentPackage>> ListByStudentAsync(Guid studentId, CancellationToken ct = default);
    Task AddAsync(StudentPackage studentPackage, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<StudentPackage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudentPackage?> GetByAsaasSubscriptionIdAsync(string subscriptionId, CancellationToken ct = default);

    // Pacote recorrente ativo OU na fila do aluno (o que o webhook confirma/cobra), se houver.
    Task<StudentPackage?> GetSubscriptionByStudentAsync(Guid studentId, CancellationToken ct = default);

    // Ids elegíveis pro job de renovação cobrar agora — ver RecurringRenewalService.
    Task<List<Guid>> ListDueForRenewalAsync(DateTime now, CancellationToken ct = default);

    Task<SubscriptionListPage> ListSubscriptionsPagedAsync(
        SubscriptionListFilter filter, int page, int pageSize, CancellationToken ct = default);

    // Assinatura ativa/na fila de cada aluno da lista, batched (evita N+1 na listagem de alunos
    // do ERP quando ela precisa mostrar o paymentStatus de cada um).
    Task<IEnumerable<StudentPackage>> ListSubscriptionsByStudentIdsAsync(
        IEnumerable<Guid> studentIds, CancellationToken ct = default);

    // Batch por id, com Package — usado pra resolver packageName sem N+1 (ex: GET /api/alerts).
    Task<IEnumerable<StudentPackage>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}