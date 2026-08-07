using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IItineraryRepository"/>.</summary>
public sealed class SqlItineraryRepository(TripSideKickDbContext dbContext) : IItineraryRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<ItineraryItem?> GetAsync(ItineraryItemId id, CancellationToken cancellationToken = default) =>
        dbContext.ItineraryItems.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ItineraryItem>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        await dbContext.ItineraryItems
            .AsNoTracking()
            .Where(i => i.TripId == tripId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(ItineraryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await dbContext.ItineraryItems.AddAsync(item, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ItineraryItem item, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        if (dbContext.Entry(item).State == EntityState.Detached)
        {
            dbContext.ItineraryItems.Attach(item);
        }

        dbContext.Entry(item).Property(i => i.RowVersion!).OriginalValue = expectedRowVersion;
        dbContext.Entry(item).State = EntityState.Modified;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The itinerary item was changed by someone else. Reload it and try again.");
        }
    }

    public async Task RemoveAsync(ItineraryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (dbContext.Entry(item).State == EntityState.Detached)
        {
            dbContext.ItineraryItems.Attach(item);
        }

        dbContext.ItineraryItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
