using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Trips;

/// <summary>
/// Persistence boundary for the <see cref="Trip"/> aggregate.
/// </summary>
/// <remarks>
/// Implementations live in <c>MX.TripSideKick.Infrastructure</c> and are cache-first per
/// <c>patterns.repository.instructions.md</c>. Application services depend on this interface only —
/// never on <c>DbContext</c> or an Azure SDK client.
/// </remarks>
public interface ITripRepository
{
    Task<Trip?> GetAsync(TripId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Trip>> GetManyAsync(IReadOnlyCollection<TripId> ids, CancellationToken cancellationToken = default);

    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing trip, enforcing optimistic concurrency against
    /// <paramref name="expectedRowVersion"/>. Throws
    /// <see cref="Common.ConcurrencyConflictException"/> when the stored row version has moved on.
    /// </summary>
    Task UpdateAsync(Trip trip, byte[] expectedRowVersion, CancellationToken cancellationToken = default);
}
