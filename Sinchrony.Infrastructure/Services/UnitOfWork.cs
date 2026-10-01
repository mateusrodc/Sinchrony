using Microsoft.EntityFrameworkCore;
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

    public async Task AcquireAdvisoryLockAsync(string key, CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("AcquireAdvisoryLockAsync exige uma transação aberta.");

        // hashtextextended devolve bigint (chave de 64 bits) — menos colisão que hashtext (32 bits).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
}
