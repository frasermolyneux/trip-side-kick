namespace MX.TripSideKick.Domain.Travellers;

/// <summary>
/// How a member's persistent trip-scoped traveller filter narrows down which items they see.
/// </summary>
public enum TravellerFilterMode
{
    /// <summary>Show every item that applies to anyone on the trip (the default).</summary>
    Everyone = 0,

    /// <summary>Show only items applicable to the traveller linked to this member's own membership.</summary>
    Me = 1,

    /// <summary>Show only items applicable to at least one of a hand-picked set of travellers.</summary>
    Selected = 2
}
