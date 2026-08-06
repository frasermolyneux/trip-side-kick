using MX.TripSideKick.Domain.Itinerary;

namespace MX.TripSideKick.Application.Itinerary;

/// <summary>Persistence boundary for append-only <see cref="ItineraryComment"/>s.</summary>
public interface IItineraryCommentRepository
{
    Task<IReadOnlyList<ItineraryComment>> ListForItemAsync(
        ItineraryItemId itineraryItemId, CancellationToken cancellationToken = default);

    Task AddAsync(ItineraryComment comment, CancellationToken cancellationToken = default);

    Task RemoveAllForItemAsync(ItineraryItemId itineraryItemId, CancellationToken cancellationToken = default);
}
