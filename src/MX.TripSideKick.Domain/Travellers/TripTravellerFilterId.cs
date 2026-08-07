namespace MX.TripSideKick.Domain.Travellers;

/// <summary>Strongly-typed identifier for a <see cref="TripTravellerFilter"/>.</summary>
public readonly record struct TripTravellerFilterId(Guid Value)
{
    public static TripTravellerFilterId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
