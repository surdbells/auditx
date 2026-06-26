namespace AuditX.Application.Abstractions;

/// <summary>
/// Commits all pending changes (entity mutations plus enqueued audit-trail entries) in a single
/// database transaction, guaranteeing a state change and its audit evidence commit atomically.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Run <paramref name="action"/> within an explicit transaction (used for replay-on-approve flows).</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
