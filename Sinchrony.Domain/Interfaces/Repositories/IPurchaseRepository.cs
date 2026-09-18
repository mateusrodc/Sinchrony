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
}