using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Domain.Memberships;

/// <summary>
/// Membership aggregate root: a signed-in identity's role on a trip. Journey 2 ("plan together").
/// </summary>
/// <remarks>
/// Authorization is always keyed on <c>SubjectId</c> (the Entra object id) - never email, which is
/// mutable and PII. See docs/identity-and-access.md.
/// </remarks>
public sealed class Membership
{
    private Membership()
    {
    }

    public MembershipId Id { get; private init; }

    public TripId TripId { get; private init; }

    /// <summary>The stable Entra object id (<c>oid</c>) of the member. Never email.</summary>
    public string SubjectId { get; private init; } = string.Empty;

    public MembershipRole Role { get; private set; }

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    public static Membership Create(TripId tripId, string subjectId, MembershipRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        return new Membership
        {
            Id = MembershipId.New(),
            TripId = tripId,
            SubjectId = subjectId,
            Role = role
        };
    }

    public void ChangeRole(MembershipRole newRole) => Role = newRole;
}
