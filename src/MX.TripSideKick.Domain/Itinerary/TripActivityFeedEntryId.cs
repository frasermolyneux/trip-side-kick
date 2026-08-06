namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>Strongly-typed identifier for a <see cref="TripActivityFeedEntry"/>.</summary>
public readonly record struct TripActivityFeedEntryId(Guid Value)
{
    public static TripActivityFeedEntryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
