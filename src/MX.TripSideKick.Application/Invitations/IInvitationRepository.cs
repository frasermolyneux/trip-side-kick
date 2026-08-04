using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Invitations;

/// <summary>
/// Persistence boundary for the <see cref="Invitation"/> aggregate.
/// </summary>
public interface IInvitationRepository
{
    Task<Invitation?> GetAsync(InvitationId id, CancellationToken cancellationToken = default);

    Task<Invitation?> GetByAcceptanceTokenAsync(Guid acceptanceToken, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invitation>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default);

    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);

    Task UpdateAsync(Invitation invitation, byte[] expectedRowVersion, CancellationToken cancellationToken = default);
}
