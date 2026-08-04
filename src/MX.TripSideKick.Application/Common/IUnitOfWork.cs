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
}
