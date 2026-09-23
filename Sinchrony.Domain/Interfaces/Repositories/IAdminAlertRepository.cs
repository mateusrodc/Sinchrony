using Sinchrony.Domain.Entities;

namespace Sinchrony.Domain.Interfaces.Repositories;

public record AdminAlertListPage(IReadOnlyList<AdminAlert> Items, int Total, int UnreadCount);

public interface IAdminAlertRepository
{
    Task<bool> ExistsByTransactionIdAsync(string transactionId, CancellationToken ct = default);
    Task AddAsync(AdminAlert alert, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task<AdminAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AdminAlertListPage> ListPagedAsync(bool? unread, int page, int pageSize, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}
