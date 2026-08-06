using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITripActivityFeedRepository"/>.</summary>
public sealed class SqlTripActivityFeedRepository(TripSideKickDbContext dbContext) : ITripActivityFeedRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<IReadOnlyList<TripActivityFeedEntry>> ListForTripAsync(TripId tripId, int maxEntries, CancellationToken cancellationToken = default) =>
        await dbContext.TripActivityFeedEntries
            .AsNoTracking()
            .Where(e => e.TripId == tripId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(maxEntries)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(TripActivityFeedEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await dbContext.TripActivityFeedEntries.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
