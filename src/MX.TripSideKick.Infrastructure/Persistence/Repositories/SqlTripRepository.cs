using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

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

    public async Task<IReadOnlyList<Trip>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Trips
            .AsNoTracking()
            .OrderBy(t => t.StartDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task UpsertAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var exists = await dbContext.Trips
            .AsNoTracking()
            .AnyAsync(t => t.Id == trip.Id, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            dbContext.Trips.Update(trip);
        }
        else
        {
            await dbContext.Trips.AddAsync(trip, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        cache.Remove(CacheKey(trip.Id));
    }

    private static string CacheKey(TripId id) => $"trip:{id.Value}";
}
