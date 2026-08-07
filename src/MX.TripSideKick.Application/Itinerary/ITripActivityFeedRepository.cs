using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Itinerary;

/// <summary>Persistence boundary for the append-only trip activity feed.</summary>
public interface ITripActivityFeedRepository
{
    /// <summary>
    /// Returns the trip's most recent activity-feed entries in reverse chronological order
    /// (newest first), capped at <paramref name="maxEntries"/>.
    /// </summary>
    Task<IReadOnlyList<TripActivityFeedEntry>> ListForTripAsync(
        TripId tripId, int maxEntries, CancellationToken cancellationToken = default);

    Task AddAsync(TripActivityFeedEntry entry, CancellationToken cancellationToken = default);
}
