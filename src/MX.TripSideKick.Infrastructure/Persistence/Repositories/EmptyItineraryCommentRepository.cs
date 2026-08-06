using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Domain.Itinerary;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Neutral no-op implementation used when SQL is not configured.</summary>
public sealed class EmptyItineraryCommentRepository : IItineraryCommentRepository
{
    public Task<IReadOnlyList<ItineraryComment>> ListForItemAsync(ItineraryItemId itineraryItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ItineraryComment>>(Array.Empty<ItineraryComment>());

    public Task AddAsync(ItineraryComment comment, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Itinerary comment storage is not configured. Set 'Sql:ConnectionString' to enable it.");

    public Task RemoveAllForItemAsync(ItineraryItemId itineraryItemId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
