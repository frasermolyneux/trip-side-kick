using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Travellers;

/// <summary>
/// Application service for the minimal traveller list - who a trip is being planned <em>for</em>.
/// Full per-activity assignment/filtering is deferred to a later "Journey 10" slice.
/// </summary>
public sealed class TravellerService(ITravellerRepository travellerRepository, MembershipAccessService membershipAccess)
{
    private readonly ITravellerRepository travellerRepository = travellerRepository
        ?? throw new ArgumentNullException(nameof(travellerRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess
        ?? throw new ArgumentNullException(nameof(membershipAccess));

    public async Task<IReadOnlyList<Traveller>> ListTravellersAsync(
        TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken).ConfigureAwait(false);
        return await travellerRepository.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Links the caller as a traveller on their own membership. Any member may add themselves.</summary>
    public async Task<Traveller> LinkSelfAsTravellerAsync(
        TripId tripId, string subjectId, string displayName, CancellationToken cancellationToken = default)
    {
        var membership = await membershipAccess
            .RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken)
            .ConfigureAwait(false);

        var existing = await travellerRepository
            .GetLinkedToMembershipAsync(membership.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            throw new AlreadyMemberException("You are already linked as a traveller on this trip.");
        }

        var traveller = Traveller.Create(tripId, displayName, membership.Id);
        await travellerRepository.AddAsync(traveller, cancellationToken).ConfigureAwait(false);
        return traveller;
    }

    /// <summary>
    /// Removes the caller as a traveller without affecting their membership/role - an Owner can do
    /// this and keeps ownership of the trip.
    /// </summary>
    public async Task UnlinkSelfAsTravellerAsync(TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipAccess
            .RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken)
            .ConfigureAwait(false);

        var traveller = await travellerRepository
            .GetLinkedToMembershipAsync(membership.Id, cancellationToken)
            .ConfigureAwait(false);

        if (traveller is null)
        {
            throw new NotFoundException("You are not linked as a traveller on this trip.");
        }

        await travellerRepository.RemoveAsync(traveller, cancellationToken).ConfigureAwait(false);
    }
}
