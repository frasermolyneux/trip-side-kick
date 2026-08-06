namespace MX.TripSideKick.Domain.Memberships;

/// <summary>
/// Strongly-typed identifier for a <see cref="Membership"/>.
/// </summary>
public readonly record struct MembershipId(Guid Value)
{
    public static MembershipId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
