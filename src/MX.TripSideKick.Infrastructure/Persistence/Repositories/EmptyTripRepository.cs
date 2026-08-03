using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>
/// Empty repository used when no SQL connection string is configured.
/// </summary>
/// <remarks>
/// The walking skeleton deploys without a database connection, and startup must not depend on SQL.
/// This keeps the DI graph valid and makes the "no data yet" behaviour explicit rather than a
/// missing-registration crash. It is replaced by <see cref="SqlTripRepository"/> as soon as a
/// connection string is present.
/// </remarks>
public sealed class EmptyTripRepository : ITripRepository
{
    public Task<Trip?> GetAsync(TripId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Trip?>(null);

    public Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>([]);

    public Task UpsertAsync(Trip trip, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Trip persistence is not configured. Set the 'Sql:ConnectionString' setting to enable it.");
}
