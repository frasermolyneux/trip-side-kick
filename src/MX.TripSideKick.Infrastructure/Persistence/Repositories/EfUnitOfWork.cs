using System.Data;

using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Infrastructure.Persistence;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>
/// Wraps a group of repository calls (each of which calls <c>SaveChangesAsync</c> itself) in a
/// single SQL transaction against the shared, per-request <see cref="TripSideKickDbContext"/>.
/// </summary>
public sealed class EfUnitOfWork(TripSideKickDbContext dbContext) : IUnitOfWork
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteWithIsolationAsync(operation, isolationLevel: null, cancellationToken);

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync<object?>(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        ExecuteWithIsolationAsync(operation, IsolationLevel.Serializable, cancellationToken);

    public async Task ExecuteSerializableAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteSerializableAsync<object?>(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TResult> ExecuteWithIsolationAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = isolationLevel is { } level
                ? await dbContext.Database.BeginTransactionAsync(level, cancellationToken).ConfigureAwait(false)
                : await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var result = await operation(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }).ConfigureAwait(false);
    }
}
