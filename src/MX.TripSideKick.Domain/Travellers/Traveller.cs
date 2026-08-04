using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Domain.Travellers;

/// <summary>
/// Minimal traveller aggregate: someone the trip is being planned <em>for</em>, as opposed to
/// someone with app access to plan it (a <see cref="Membership"/>). Kept deliberately small - full
/// per-activity assignment and the Everyone/Me/selected filter are deferred to a later "Journey
/// 10" slice; <see cref="LinkedMembershipId"/> is the seam that slice will build on.
/// </summary>
public sealed class Traveller
{
    private Traveller()
    {
    }

    public TravellerId Id { get; private init; }

    public TripId TripId { get; private init; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// The account this traveller is linked to, if any. A trip's creator is linked to their own
    /// Owner membership by default; a non-travelling planner has a membership with no traveller at
    /// all. Never more than one traveller may link to the same membership.
    /// </summary>
    public MembershipId? LinkedMembershipId { get; private set; }

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    public static Traveller Create(TripId tripId, string displayName, MembershipId? linkedMembershipId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new Traveller
        {
            Id = TravellerId.New(),
            TripId = tripId,
            DisplayName = displayName,
            LinkedMembershipId = linkedMembershipId
        };
    }

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
    }

    public void LinkToMembership(MembershipId membershipId) => LinkedMembershipId = membershipId;

    public void UnlinkFromMembership() => LinkedMembershipId = null;
}
