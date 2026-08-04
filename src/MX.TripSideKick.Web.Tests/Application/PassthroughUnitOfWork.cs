using MX.TripSideKick.Application.Common;

namespace MX.TripSideKick.Web.Tests.Application;

/// <summary>
/// A test double for <see cref="IUnitOfWork"/> that just invokes the operation directly - no real
/// transaction, since these are application-service unit tests against mocked repositories, not
/// integration tests against a real database.
/// </summary>
internal sealed class PassthroughUnitOfWork : IUnitOfWork
{
    public Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);

    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}
