using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Neutral no-op implementation used when SQL is not configured.</summary>
public sealed class EmptyItineraryRepository : IItineraryRepository
{
    public Task<ItineraryItem?> GetAsync(ItineraryItemId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<ItineraryItem?>(null);

    public Task<IReadOnlyList<ItineraryItem>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ItineraryItem>>(Array.Empty<ItineraryItem>());

    public Task AddAsync(ItineraryItem item, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task UpdateAsync(ItineraryItem item, byte[] expectedRowVersion, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task RemoveAsync(ItineraryItem item, CancellationToken cancellationToken = default) => throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("Itinerary storage is not configured. Set 'Sql:ConnectionString' to enable it.");
}
