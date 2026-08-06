namespace MX.TripSideKick.Domain.Trips;

/// <summary>
/// Whether a trip's dates are known well enough to support day-by-day planning.
/// </summary>
public enum TripDateStatus
{
    /// <summary>No dates have been chosen yet. Planning is high-level only.</summary>
    Undecided = 0,

    /// <summary>A rough window is known (e.g. "sometime in June") but not exact days.</summary>
    Approximate = 1,

    /// <summary>Exact start/end dates are confirmed; day-by-day scheduling can be enabled.</summary>
    Confirmed = 2
}
