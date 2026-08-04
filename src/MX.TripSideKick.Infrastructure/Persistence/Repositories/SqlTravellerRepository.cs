using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using static MX.TripSideKick.Infrastructure.Persistence.Repositories.SqlExceptionHelpers;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITravellerRepository"/>.</summary>
public sealed class SqlTravellerRepository(TripSideKickDbContext dbContext) : ITravellerRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<Traveller?> GetAsync(TravellerId id, CancellationToken cancellationToken = default) =>
        dbContext.Travellers.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Traveller>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        await dbContext.Travellers
            .AsNoTracking()
            .Where(t => t.TripId == tripId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Traveller?> GetLinkedToMembershipAsync(MembershipId membershipId, CancellationToken cancellationToken = default) =>
        dbContext.Travellers.AsNoTracking().SingleOrDefaultAsync(t => t.LinkedMembershipId == membershipId, cancellationToken);

    public async Task AddAsync(Traveller traveller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(traveller);

        await dbContext.Travellers.AddAsync(traveller, cancellationToken).ConfigureAwait(false);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent request already linked another traveller to this membership (the unique
            // index on LinkedMembershipId enforces "one traveller per membership") - translate the
            // raw unique-index violation into the same domain conflict the application-level
            // check-then-act paths in TravellerService/InvitationService already raise.
            throw new AlreadyMemberException("That traveller is already linked to a member.");
        }
    }

    public async Task UpdateAsync(Traveller traveller, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(traveller);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        dbContext.Travellers.Attach(traveller);
        dbContext.Entry(traveller).Property(t => t.RowVersion!).OriginalValue = expectedRowVersion;
        dbContext.Entry(traveller).State = EntityState.Modified;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The traveller was changed by someone else. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent request already linked this (or another) membership to the same
            // traveller row - see AddAsync's comment for the same race on insert.
            throw new AlreadyMemberException("That traveller is already linked to a member.");
        }
    }

    public async Task RemoveAsync(Traveller traveller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(traveller);

        dbContext.Travellers.Attach(traveller);
        dbContext.Travellers.Remove(traveller);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
