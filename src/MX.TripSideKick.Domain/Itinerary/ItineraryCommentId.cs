namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>Strongly-typed identifier for an <see cref="ItineraryComment"/>.</summary>
public readonly record struct ItineraryCommentId(Guid Value)
{
    public static ItineraryCommentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
