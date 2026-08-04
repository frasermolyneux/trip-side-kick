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

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var result = await operation(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await ExecuteAsync<object?>(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }
}
