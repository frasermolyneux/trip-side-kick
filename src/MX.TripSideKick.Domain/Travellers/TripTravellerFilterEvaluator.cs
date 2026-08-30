namespace MX.TripSideKick.Domain.Travellers;

/// <summary>
/// Pure filtering rule: given a member's chosen <see cref="TravellerFilterMode"/>, an already-
/// resolved "effective" traveller-id set (empty in <see cref="TravellerFilterMode.Everyone"/> mode
/// or when the caller's membership isn't linked to any traveller in
/// <see cref="TravellerFilterMode.Me"/> mode), and the applicable-traveller-id list of a candidate
/// item, decide whether that item should be visible.
/// </summary>
/// <remarks>
/// Deliberately independent of any specific aggregate (items today, bookings/costs tomorrow):
/// callers pass in the item's own applicable-traveller-id list, so this same predicate can gate any
/// future surface that stores its own applicability the same way.
/// </remarks>
public static class TripTravellerFilterEvaluator
{
    public static bool IsVisible(
        TravellerFilterMode mode,
        IReadOnlyCollection<TravellerId> effectiveTravellerIds,
        IReadOnlyCollection<TravellerId> itemApplicableTravellerIds)
    {
        ArgumentNullException.ThrowIfNull(effectiveTravellerIds);
        ArgumentNullException.ThrowIfNull(itemApplicableTravellerIds);

        if (mode == TravellerFilterMode.Everyone)
        {
            return true;
        }

        if (TravellerApplicability.AppliesToEveryone(itemApplicableTravellerIds))
        {
            return true;
        }

        // Me/Selected both collapse to "does the item intersect this effective set?". An "empty"
        // effective set happens when Me mode is chosen by a member who isn't linked to any
        // traveller yet - in which case nothing traveller-scoped can possibly apply to them.
        if (effectiveTravellerIds.Count == 0)
        {
            return false;
        }

        return TravellerApplicability.Intersects(itemApplicableTravellerIds, effectiveTravellerIds);
    }
}
