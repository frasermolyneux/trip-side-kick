namespace MX.TripSideKick.Domain.Travellers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Traveller"/>.
/// </summary>
public readonly record struct TravellerId(Guid Value)
{
    public static TravellerId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
