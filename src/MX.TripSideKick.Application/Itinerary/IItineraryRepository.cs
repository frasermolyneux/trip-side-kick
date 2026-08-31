using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Itinerary;

/// <summary>Persistence boundary for the <see cref="ItineraryItem"/> aggregate.</summary>
public interface IItineraryRepository
{
    Task<ItineraryItem?> GetAsync(ItineraryItemId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItineraryItem>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default);

    Task AddAsync(ItineraryItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing item, enforcing optimistic concurrency against
    /// <paramref name="expectedRowVersion"/>. Throws
    /// <see cref="Common.ConcurrencyConflictException"/> when the stored row version has moved on.
    /// </summary>
    Task UpdateAsync(ItineraryItem item, byte[] expectedRowVersion, CancellationToken cancellationToken = default);

    Task RemoveAsync(ItineraryItem item, CancellationToken cancellationToken = default);
}
