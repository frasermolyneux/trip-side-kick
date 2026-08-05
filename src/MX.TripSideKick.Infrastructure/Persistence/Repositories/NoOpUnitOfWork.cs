using MX.TripSideKick.Application.Common;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>
/// No-op unit of work used when no SQL connection string is configured. Repository calls made
/// through the <c>Empty*</c> repositories throw before this would matter, so there is nothing to
/// wrap transactionally.
/// </summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation(cancellationToken);
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation(cancellationToken);
    }

    public Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation(cancellationToken);
    }

    public Task ExecuteSerializableAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return operation(cancellationToken);
    }
}
