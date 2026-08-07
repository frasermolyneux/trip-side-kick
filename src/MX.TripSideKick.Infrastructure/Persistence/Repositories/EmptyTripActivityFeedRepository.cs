using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Neutral no-op implementation used when SQL is not configured.</summary>
public sealed class EmptyTripActivityFeedRepository : ITripActivityFeedRepository
{
    public Task<IReadOnlyList<TripActivityFeedEntry>> ListForTripAsync(TripId tripId, int maxEntries, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TripActivityFeedEntry>>(Array.Empty<TripActivityFeedEntry>());

    public Task AddAsync(TripActivityFeedEntry entry, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Trip activity feed storage is not configured. Set 'Sql:ConnectionString' to enable it.");
}
