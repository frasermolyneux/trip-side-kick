using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Travellers;

/// <summary>
/// Reads and updates a member's persistent trip-scoped traveller filter (Everyone/Me/Selected) and
/// resolves that preference to the "effective" traveller-id set the itinerary layer (and any future
/// consumer) will pass to <see cref="TripTravellerFilterEvaluator.IsVisible"/>.
/// </summary>
public sealed class TripTravellerFilterService(
    ITripTravellerFilterRepository filterRepository,
    ITravellerRepository travellerRepository,
    MembershipAccessService membershipAccess)
{
    private readonly ITripTravellerFilterRepository filterRepository = filterRepository ?? throw new ArgumentNullException(nameof(filterRepository));
    private readonly ITravellerRepository travellerRepository = travellerRepository ?? throw new ArgumentNullException(nameof(travellerRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess ?? throw new ArgumentNullException(nameof(membershipAccess));

    /// <summary>
    /// Returns the caller's persisted filter for the trip, auto-creating the default (Everyone)
    /// row on first read so callers never have to distinguish "not set yet" from "explicitly
    /// Everyone".
    /// </summary>
    public async Task<TripTravellerFilter> GetOrCreateForCallerAsync(
        TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipAccess
            .RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken)
            .ConfigureAwait(false);

        var filter = await filterRepository
            .GetForTripAndMembershipAsync(tripId, membership.Id, cancellationToken)
            .ConfigureAwait(false);
        if (filter is not null)
        {
            return filter;
        }

        var created = TripTravellerFilter.CreateDefault(tripId, membership.Id);
        try
        {
            await filterRepository.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return created;
        }
        catch (ConcurrencyConflictException)
        {
            // A concurrent request for the same member created the row first - the unique index on
            // (TripId, MembershipId) means we can safely re-read and return that one.
            return await filterRepository
                .GetForTripAndMembershipAsync(tripId, membership.Id, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Traveller filter row disappeared immediately after a concurrency conflict.");
        }
    }

    /// <summary>
    /// Updates the caller's persisted filter. Auto-creates the row on first write if the caller
    /// has never read it (so a first-visit "set my filter" call still works).
    /// </summary>
    public async Task<TripTravellerFilter> UpdateForCallerAsync(
        TripId tripId,
        string subjectId,
        TravellerFilterMode mode,
        IReadOnlyList<TravellerId>? selectedTravellerIds,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        var membership = await membershipAccess
            .RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken)
            .ConfigureAwait(false);

        var filter = await filterRepository
            .GetForTripAndMembershipAsync(tripId, membership.Id, cancellationToken)
            .ConfigureAwait(false);

        if (filter is null)
        {
            // First-write auto-create: create a default row, then update it in place so the row
            // ends up with the requested mode/selection and a real RowVersion.
            var created = TripTravellerFilter.CreateDefault(tripId, membership.Id);
            try
            {
                await filterRepository.AddAsync(created, cancellationToken).ConfigureAwait(false);
                filter = created;
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent first-write for the same member created the row first (races on the
                // unique (TripId, MembershipId) index). The caller couldn't have supplied a valid
                // If-Match for a row it didn't know existed, so re-read the winning row and apply
                // this caller's requested mode/selection against its real RowVersion instead of
                // discarding the request with a 409 the caller has no way to recover from.
                filter = await filterRepository
                    .GetForTripAndMembershipAsync(tripId, membership.Id, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "Traveller filter row disappeared immediately after a concurrency conflict.");
                expectedRowVersion = filter.RowVersion
                    ?? throw new InvalidOperationException("Persisted traveller filter is missing a RowVersion.");
            }
        }

        filter.Update(mode, selectedTravellerIds);
        await filterRepository.UpdateAsync(filter, expectedRowVersion, cancellationToken).ConfigureAwait(false);

        return filter;
    }

    /// <summary>
    /// Resolves the caller's filter to the "effective" traveller-id set that
    /// <see cref="TripTravellerFilterEvaluator.IsVisible"/> expects. Callers that already have the
    /// filter loaded can call this directly with it (there's no separate DB round trip here beyond
    /// the traveller lookup <see cref="TravellerFilterMode.Me"/> needs).
    /// </summary>
    public async Task<IReadOnlyCollection<TravellerId>> ResolveEffectiveTravellerIdsAsync(
        TripTravellerFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter.Mode switch
        {
            TravellerFilterMode.Everyone => [],
            TravellerFilterMode.Selected => filter.SelectedTravellerIds.ToArray(),
            TravellerFilterMode.Me => await ResolveMeAsync(filter.MembershipId, cancellationToken).ConfigureAwait(false),
            _ => []
        };
    }

    private async Task<IReadOnlyCollection<TravellerId>> ResolveMeAsync(
        MembershipId membershipId, CancellationToken cancellationToken)
    {
        var traveller = await travellerRepository
            .GetLinkedToMembershipAsync(membershipId, cancellationToken)
            .ConfigureAwait(false);
        return traveller is null ? [] : [traveller.Id];
    }
}
