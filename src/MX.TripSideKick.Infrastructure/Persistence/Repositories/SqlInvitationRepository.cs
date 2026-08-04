using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IInvitationRepository"/>.</summary>
public sealed class SqlInvitationRepository(TripSideKickDbContext dbContext) : IInvitationRepository
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<Invitation?> GetAsync(InvitationId id, CancellationToken cancellationToken = default) =>
        dbContext.Invitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Invitation?> GetByAcceptanceTokenAsync(Guid acceptanceToken, CancellationToken cancellationToken = default) =>
        dbContext.Invitations.AsNoTracking().SingleOrDefaultAsync(i => i.AcceptanceToken == acceptanceToken, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        await dbContext.Invitations
            .AsNoTracking()
            .Where(i => i.TripId == tripId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        await dbContext.Invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Invitation invitation, byte[] expectedRowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        dbContext.Invitations.Attach(invitation);
        dbContext.Entry(invitation).Property(i => i.RowVersion!).OriginalValue = expectedRowVersion;
        dbContext.Entry(invitation).State = EntityState.Modified;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The invitation was changed by someone else. Reload it and try again.");
        }
    }
}
