using Microsoft.EntityFrameworkCore.Storage;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence;

namespace Sinchrony.Infrastructure.Services;

public class UnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await db.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}