namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>
/// Whether an <see cref="ItineraryItem"/> is a floating idea or has been placed onto a specific day.
/// </summary>
/// <remarks>
/// Modelled with an explicit status (rather than a nullable <c>Date</c> alone) for the same reason
/// <see cref="Trips.TripDateStatus"/> exists: "the traveller didn't schedule this yet" reads
/// differently from "scheduled, but I haven't loaded it" and the transition between the two is
/// something we deliberately want to be able to name.
/// </remarks>
public enum ItineraryScheduleStatus
{
    /// <summary>A parked idea. No date/time is set; the item shows up in the "ideas" pool.</summary>
    Unscheduled = 0,

    /// <summary>Promoted onto a specific day (and optionally a time range) on the trip's itinerary.</summary>
    Scheduled = 1
}
