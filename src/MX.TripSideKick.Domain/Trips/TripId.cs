namespace MX.TripSideKick.Domain.Trips;

/// <summary>
/// Strongly-typed identifier for a <see cref="Trip"/>.
/// </summary>
/// <remarks>
/// Identifiers are UUIDv7 so that they sort by creation time — this keeps clustered index
/// inserts sequential in Azure SQL while remaining opaque to clients.
/// </remarks>
public readonly record struct TripId(Guid Value)
{
    public static TripId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
