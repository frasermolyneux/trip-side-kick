using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using static MX.TripSideKick.Infrastructure.Persistence.Repositories.SqlExceptionHelpers;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITripTravellerFilterRepository"/>.</summary>
public sealed class SqlTripTravellerFilterRepository(TripSideKickDbContext dbContext) : ITripTravellerFilterRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<TripTravellerFilter?> GetForTripAndMembershipAsync(TripId tripId, MembershipId membershipId, CancellationToken cancellationToken = default) =>
        dbContext.TripTravellerFilters.SingleOrDefaultAsync(f => f.TripId == tripId && f.MembershipId == membershipId, cancellationToken);

    public async Task AddAsync(TripTravellerFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await dbContext.TripTravellerFilters.AddAsync(filter, cancellationToken).ConfigureAwait(false);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Two concurrent first-reads for the same member raced on the unique index -
            // the caller is expected to re-read and use the winning row.
            throw new ConcurrencyConflictException(
                "The traveller filter was created by another request; reload it and try again.");
        }
    }

    public async Task UpdateAsync(TripTravellerFilter filter, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        if (dbContext.Entry(filter).State == EntityState.Detached)
        {
            dbContext.TripTravellerFilters.Attach(filter);
        }

        dbContext.Entry(filter).Property(f => f.RowVersion!).OriginalValue = expectedRowVersion;
        dbContext.Entry(filter).State = EntityState.Modified;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The traveller filter was changed by someone else. Reload it and try again.");
        }
    }
}
