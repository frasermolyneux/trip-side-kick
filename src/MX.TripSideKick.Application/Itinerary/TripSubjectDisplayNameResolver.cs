using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Itinerary;

/// <summary>
/// Resolves opaque subject ids into user-facing display names for the itinerary UI, using the
/// traveller linked to each subject's membership on the trip. Falls back to a generic label when
/// the subject is not linked to any traveller — the raw subject id is never returned. This
/// matches the identity-hygiene rule from <c>AGENTS.md</c>: "never expose the raw subjectId to
/// the client, never resolve via email".
/// </summary>
public sealed class TripSubjectDisplayNameResolver(
    IMembershipRepository memberships,
    ITravellerRepository travellers)
{
    public const string FallbackDisplayName = "Trip member";

    private readonly IMembershipRepository memberships = memberships ?? throw new ArgumentNullException(nameof(memberships));
    private readonly ITravellerRepository travellers = travellers ?? throw new ArgumentNullException(nameof(travellers));

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        TripId tripId, IEnumerable<string> subjectIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectIds);

        var distinct = subjectIds.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var tripMemberships = await memberships.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
        var tripTravellers = await travellers.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);

        var membershipBySubject = tripMemberships.ToDictionary(m => m.SubjectId, m => m.Id, StringComparer.Ordinal);
        var travellerByMembership = tripTravellers
            .Where(t => t.LinkedMembershipId is not null)
            .ToDictionary(t => t.LinkedMembershipId!.Value, t => t.DisplayName);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var subjectId in distinct)
        {
            if (membershipBySubject.TryGetValue(subjectId, out var membershipId)
                && travellerByMembership.TryGetValue(membershipId, out var displayName))
            {
                result[subjectId] = displayName;
            }
            else
            {
                result[subjectId] = FallbackDisplayName;
            }
        }

        return result;
    }
}
