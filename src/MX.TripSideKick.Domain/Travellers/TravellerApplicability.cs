namespace MX.TripSideKick.Domain.Travellers;

/// <summary>
/// Pure, IO-free helpers for the "which travellers does this thing apply to?" question, using the
/// empty-list-means-everyone encoding.
/// </summary>
/// <remarks>
/// Reuse seam for future slices: any aggregate that carries its own
/// <c>IReadOnlyCollection&lt;TravellerId&gt;</c> of applicable travellers (bookings, costs,
/// documents, packing items ...) can call the same three predicates below without duplicating the
/// "empty means everyone" special case. See <c>docs/itinerary-and-travellers.md</c> for the design
/// rationale.
/// </remarks>
public static class TravellerApplicability
{
    /// <summary>True when <paramref name="applicableTravellerIds"/> encodes "applies to everyone".</summary>
    public static bool AppliesToEveryone(IReadOnlyCollection<TravellerId> applicableTravellerIds)
    {
        ArgumentNullException.ThrowIfNull(applicableTravellerIds);
        return applicableTravellerIds.Count == 0;
    }

    /// <summary>
    /// True when the thing described by <paramref name="applicableTravellerIds"/> applies to
    /// <paramref name="travellerId"/> - either because it applies to everyone, or because
    /// <paramref name="travellerId"/> is explicitly in the list.
    /// </summary>
    public static bool AppliesTo(
        IReadOnlyCollection<TravellerId> applicableTravellerIds, TravellerId travellerId)
    {
        ArgumentNullException.ThrowIfNull(applicableTravellerIds);
        return applicableTravellerIds.Count == 0 || applicableTravellerIds.Contains(travellerId);
    }

    /// <summary>
    /// True when the thing described by <paramref name="applicableTravellerIds"/> applies to at
    /// least one of the travellers in <paramref name="candidateTravellerIds"/>. "Applies to
    /// everyone" (empty <paramref name="applicableTravellerIds"/>) intersects any non-empty
    /// candidate set trivially.
    /// </summary>
    public static bool Intersects(
        IReadOnlyCollection<TravellerId> applicableTravellerIds,
        IReadOnlyCollection<TravellerId> candidateTravellerIds)
    {
        ArgumentNullException.ThrowIfNull(applicableTravellerIds);
        ArgumentNullException.ThrowIfNull(candidateTravellerIds);

        if (applicableTravellerIds.Count == 0)
        {
            return true;
        }

        foreach (var candidate in candidateTravellerIds)
        {
            if (applicableTravellerIds.Contains(candidate))
            {
                return true;
            }
        }

        return false;
    }
}
