using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Domain.Itinerary;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IItineraryCommentRepository"/>.</summary>
public sealed class SqlItineraryCommentRepository(TripSideKickDbContext dbContext) : IItineraryCommentRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<IReadOnlyList<ItineraryComment>> ListForItemAsync(ItineraryItemId itineraryItemId, CancellationToken cancellationToken = default) =>
        await dbContext.ItineraryComments
            .AsNoTracking()
            .Where(c => c.ItineraryItemId == itineraryItemId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(ItineraryComment comment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comment);
        await dbContext.ItineraryComments.AddAsync(comment, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAllForItemAsync(ItineraryItemId itineraryItemId, CancellationToken cancellationToken = default)
    {
        await dbContext.ItineraryComments
            .Where(c => c.ItineraryItemId == itineraryItemId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
