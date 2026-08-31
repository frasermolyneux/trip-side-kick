namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>Strongly-typed identifier for an <see cref="ItineraryItem"/>.</summary>
public readonly record struct ItineraryItemId(Guid Value)
{
    public static ItineraryItemId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
