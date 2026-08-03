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

    Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(Trip trip, CancellationToken cancellationToken = default);
}
