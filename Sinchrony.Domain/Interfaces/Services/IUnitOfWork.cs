namespace Sinchrony.Domain.Interfaces.Services;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);

    // Trava de aconselhamento (advisory lock) do Postgres, válida até o fim da transação aberta por
    // BeginTransactionAsync. Serializa requisições concorrentes que disputam a mesma chave.
    Task AcquireAdvisoryLockAsync(string key, CancellationToken ct = default);
}