using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

using static MX.TripSideKick.Infrastructure.Persistence.Repositories.SqlExceptionHelpers;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IMembershipRepository"/>.</summary>
public sealed class SqlMembershipRepository(TripSideKickDbContext dbContext) : IMembershipRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<Membership?> GetAsync(MembershipId id, CancellationToken cancellationToken = default) =>
        dbContext.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Membership?> GetForTripAndSubjectAsync(TripId tripId, string subjectId, CancellationToken cancellationToken = default) =>
        dbContext.Memberships
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.TripId == tripId && m.SubjectId == subjectId, cancellationToken);

    public async Task<IReadOnlyList<Membership>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        await dbContext.Memberships
            .AsNoTracking()
            .Where(m => m.TripId == tripId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Membership>> ListForSubjectAsync(string subjectId, CancellationToken cancellationToken = default) =>
        await dbContext.Memberships
            .AsNoTracking()
            .Where(m => m.SubjectId == subjectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        await dbContext.Memberships.AddAsync(membership, cancellationToken).ConfigureAwait(false);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent request already created the (TripId, SubjectId) row (e.g. two
            // near-simultaneous invitation-accept calls for the same subject) - translate the raw
            // unique-index violation into the same domain conflict the application-level
            // check-then-act path in InvitationService already raises, instead of letting an
            // unhandled SqlException surface as a 500.
            throw new AlreadyMemberException("You are already a member of this trip.");
        }
    }

    public async Task UpdateAsync(Membership membership, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        dbContext.Memberships.Attach(membership);
        dbContext.Entry(membership).Property(m => m.RowVersion!).OriginalValue = expectedRowVersion;
        dbContext.Entry(membership).State = EntityState.Modified;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The membership was changed by someone else. Reload it and try again.");
        }
    }

    public async Task RemoveAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        dbContext.Memberships.Attach(membership);
        dbContext.Memberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
