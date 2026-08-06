using MX.TripSideKick.Domain.Common;

namespace MX.TripSideKick.Domain.Memberships;

/// <summary>
/// Pure, IO-free rules governing membership role changes and removal - most importantly, that a
/// trip is never left without an Owner. Callers (application services) load the full membership
/// list for a trip and pass it in; this type never touches persistence.
/// </summary>
public static class MembershipPolicy
{
    /// <summary>
    /// Throws <see cref="LastOwnerViolationException"/> if changing <paramref name="target"/> to
    /// <paramref name="newRole"/> would leave the trip with no Owner.
    /// </summary>
    public static void EnsureRoleChangeAllowed(
        IReadOnlyCollection<Membership> membersOnTrip,
        Membership target,
        MembershipRole newRole)
    {
        ArgumentNullException.ThrowIfNull(membersOnTrip);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Role != MembershipRole.Owner || newRole == MembershipRole.Owner)
        {
            return;
        }

        if (CountOwners(membersOnTrip) <= 1)
        {
            throw new LastOwnerViolationException(
                "The last Owner cannot be demoted. Promote another member to Owner first.");
        }
    }

    /// <summary>
    /// Throws <see cref="LastOwnerViolationException"/> if removing <paramref name="target"/>
    /// would leave the trip with no Owner.
    /// </summary>
    public static void EnsureRemovalAllowed(IReadOnlyCollection<Membership> membersOnTrip, Membership target)
    {
        ArgumentNullException.ThrowIfNull(membersOnTrip);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Role != MembershipRole.Owner)
        {
            return;
        }

        if (CountOwners(membersOnTrip) <= 1)
        {
            throw new LastOwnerViolationException(
                "The last Owner cannot leave or be removed. Promote another member to Owner first.");
        }
    }

    private static int CountOwners(IReadOnlyCollection<Membership> membersOnTrip) =>
        membersOnTrip.Count(member => member.Role == MembershipRole.Owner);
}
