namespace MX.TripSideKick.Application.Common;

/// <summary>
/// Runs an operation that spans more than one repository (e.g. creating a trip plus its Owner
/// membership plus its Owner traveller) as a single atomic unit.
/// </summary>
/// <remarks>
/// Implemented in <c>MX.TripSideKick.Infrastructure</c> as a SQL transaction wrapping the shared,
/// per-request <c>DbContext</c>. Each repository still calls <c>SaveChangesAsync</c> itself
/// (consistent with the rest of this codebase's repository pattern); the transaction just makes
/// those calls atomic as a group instead of committing independently.
/// </remarks>
public interface IUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="ExecuteAsync{TResult}"/>, but with a serializable transaction. Use this
    /// when the operation reads a set of rows to decide whether an invariant that spans them still
    /// holds (e.g. "at least one Owner remains") and then writes based on that decision - under the
    /// default isolation level, two concurrent operations can each read the same
    /// invariant-still-holds snapshot and both commit, violating the invariant (a write-skew
    /// anomaly). Serializable prevents that at the cost of the transaction retrying/failing under
    /// contention; <see cref="ExecuteAsync{TResult}"/> already retries transient SQL errors, which
    /// covers the deadlocks/serialization failures this can produce.
    /// </summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>Non-generic counterpart of <see cref="ExecuteSerializableAsync{TResult}"/>.</summary>
    Task ExecuteSerializableAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
