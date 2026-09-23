using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

// Filtros da listagem do ERP. Todos opcionais; UnitId restringe às compras de alunos daquela unidade.
public record PurchaseListFilter(
    DateTime? From = null,
    DateTime? ToExclusive = null,
    string? Status = null,
    string? PaymentMethod = null,
    string? StudentSearch = null,
    Guid? UnitId = null);

// TotalAmount/ConfirmedAmount somam o recorte filtrado inteiro (não só a página atual).
public record PurchaseListPage(
    IReadOnlyList<Purchase> Items, int Total, decimal TotalAmount, decimal ConfirmedAmount);

public interface IPurchaseRepository
{
    Task<PurchaseListPage> ListErpPagedAsync(
        PurchaseListFilter filter, int page, int pageSize, CancellationToken ct = default);

    Task<IEnumerable<Purchase>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Purchase>> ListAllAsync(CancellationToken ct = default);
    Task<decimal> TotalRevenueThisMonthAsync(CancellationToken ct = default);
    Task AddAsync(Purchase purchase, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<(IEnumerable<Purchase> Items, int Total)> ListByUserPagedAsync(
    Guid userId, int page, int pageSize, CancellationToken ct = default);

    // Histórico de ciclos pagos de uma assinatura recorrente (payments[] da API de status).
    Task<(IEnumerable<Purchase> Items, int Total)> ListByStudentPackagePagedAsync(
    Guid studentPackageId, int page, int pageSize, CancellationToken ct = default);

    // Já existe uma renovação (Kind=renewal) pending ou confirmed pro ciclo atual desse pacote?
    // Evita cobrar duas vezes o mesmo ciclo (RecurringRenewalService.ProcessRenewalAsync).
    Task<bool> HasActiveRenewalForCycleAsync(
        Guid studentPackageId, DateTime cycleStart, CancellationToken ct = default);

    // Renovações (Kind=renewal) ainda `pending` há mais de `olderThan` — o webhook de
    // confirmação/recusa provavelmente se perdeu (RecurringRenewalJob reconcilia essas).
    Task<IEnumerable<Purchase>> ListStalePendingRenewalsAsync(
        DateTime olderThan, CancellationToken ct = default);

    // A renovação pending mais recente desse pacote, se houver (POST /sync manual).
    Task<Purchase?> GetPendingRenewalAsync(Guid studentPackageId, CancellationToken ct = default);

    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken ct = default);
}