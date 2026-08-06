namespace MX.TripSideKick.Domain.Memberships;

/// <summary>
/// A member's permission level on a trip.
/// </summary>
/// <remarks>
/// Ordered so that <c>Role &gt;= MembershipRole.Editor</c> style comparisons work: Viewer is
/// read-only, Editor manages trip content but not membership, Owner also manages membership.
/// </remarks>
public enum MembershipRole
{
    Viewer = 0,
    Editor = 1,
    Owner = 2
}
