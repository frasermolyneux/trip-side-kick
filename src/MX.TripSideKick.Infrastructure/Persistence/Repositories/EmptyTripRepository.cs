using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>
/// Empty repository used when no SQL connection string is configured, so DI stays valid and
/// startup/readiness never touch SQL.
/// </summary>
public sealed class EmptyTripRepository : ITripRepository
{
    public Task<Trip?> GetAsync(TripId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Trip?>(null);

    public Task<IReadOnlyList<Trip>> GetManyAsync(IReadOnlyCollection<TripId> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>([]);

    public Task AddAsync(Trip trip, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task UpdateAsync(Trip trip, byte[] expectedRowVersion, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("Trip persistence is not configured. Set the 'Sql:ConnectionString' setting to enable it.");
}
