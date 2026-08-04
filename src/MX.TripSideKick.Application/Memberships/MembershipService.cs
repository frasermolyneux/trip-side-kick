using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Memberships;

/// <summary>
/// Application service for Journey 2's role/membership management: only Owners invite, remove, or
/// change roles; Editors manage content, not membership; signed-in Viewers cannot mutate anything;
/// the last Owner can never leave, be removed, or be demoted (<see cref="MembershipPolicy"/>).
/// </summary>
public sealed class MembershipService(IMembershipRepository membershipRepository, MembershipAccessService membershipAccess)
{
    private readonly IMembershipRepository membershipRepository = membershipRepository
        ?? throw new ArgumentNullException(nameof(membershipRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess
        ?? throw new ArgumentNullException(nameof(membershipAccess));

    /// <summary>Lists members. Any member (including Viewers) may see who else is on the trip.</summary>
    public async Task<IReadOnlyList<Membership>> ListMembersAsync(
        TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken).ConfigureAwait(false);
        return await membershipRepository.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Changes a member's role. Owner-only. Blocked if it would demote the last Owner.</summary>
    public async Task<Membership> ChangeRoleAsync(
        TripId tripId,
        MembershipId targetMembershipId,
        MembershipRole newRole,
        string actingSubjectId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        await membershipAccess.RequireRoleAsync(tripId, actingSubjectId, MembershipRole.Owner, cancellationToken).ConfigureAwait(false);

        var members = await membershipRepository.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
        var target = members.FirstOrDefault(m => m.Id == targetMembershipId)
            ?? throw new NotFoundException("Member not found on this trip.");

        MembershipPolicy.EnsureRoleChangeAllowed(members, target, newRole);

        target.ChangeRole(newRole);
        await membershipRepository.UpdateAsync(target, expectedRowVersion, cancellationToken).ConfigureAwait(false);
        return target;
    }

    /// <summary>Removes a member from the trip. Owner-only. Blocked if the target is the last Owner.</summary>
    public async Task RemoveMemberAsync(
        TripId tripId, MembershipId targetMembershipId, string actingSubjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, actingSubjectId, MembershipRole.Owner, cancellationToken).ConfigureAwait(false);
        await RemoveCoreAsync(tripId, targetMembershipId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A member removes themselves from the trip. Blocked if they are the last Owner.</summary>
    public async Task LeaveTripAsync(TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipAccess
            .RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken)
            .ConfigureAwait(false);

        await RemoveCoreAsync(tripId, membership.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveCoreAsync(TripId tripId, MembershipId targetMembershipId, CancellationToken cancellationToken)
    {
        var members = await membershipRepository.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
        var target = members.FirstOrDefault(m => m.Id == targetMembershipId)
            ?? throw new NotFoundException("Member not found on this trip.");

        MembershipPolicy.EnsureRemovalAllowed(members, target);

        await membershipRepository.RemoveAsync(target, cancellationToken).ConfigureAwait(false);
    }
}
