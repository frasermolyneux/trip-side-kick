using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cache-first EF Core implementation of <see cref="ITripRepository"/>.
/// </summary>
public sealed class SqlTripRepository(TripSideKickDbContext dbContext, IMemoryCache cache) : ITripRepository
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IMemoryCache cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public async Task<Trip?> GetAsync(TripId id, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey(id), out Trip? cached))
        {
            return cached;
        }

        var trip = await dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (trip is not null)
        {
            cache.Set(CacheKey(id), trip, CacheLifetime);
        }

        return trip;
    }

    public async Task<IReadOnlyList<Trip>> GetManyAsync(IReadOnlyCollection<TripId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Trips
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trip);

        await dbContext.Trips.AddAsync(trip, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Trip trip, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        dbContext.Trips.Attach(trip);
        dbContext.Entry(trip).Property(t => t.RowVersion!).OriginalValue = expectedRowVersion;
        dbContext.Entry(trip).State = EntityState.Modified;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The trip was changed by someone else. Reload it and try again.");
        }
        finally
        {
            cache.Remove(CacheKey(trip.Id));
        }
    }

    private static string CacheKey(TripId id) => $"trip:{id.Value}";
}
