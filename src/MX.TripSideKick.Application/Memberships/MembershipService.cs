using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Memberships;

/// <summary>
/// Application service for Journey 2's role/membership management: only Owners invite, remove, or
/// change roles; Editors manage content, not membership; signed-in Viewers cannot mutate anything;
/// the last Owner can never leave, be removed, or be demoted (<see cref="MembershipPolicy"/>).
/// </summary>
public sealed class MembershipService(
    IMembershipRepository membershipRepository,
    MembershipAccessService membershipAccess,
    IUnitOfWork unitOfWork)
{
    private readonly IMembershipRepository membershipRepository = membershipRepository
        ?? throw new ArgumentNullException(nameof(membershipRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess
        ?? throw new ArgumentNullException(nameof(membershipAccess));
    private readonly IUnitOfWork unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

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

        // Serializable: the last-Owner invariant spans every membership row on the trip, so the
        // read (list members) and the write (this role change) must be isolated as one unit from
        // any concurrent change/removal targeting a *different* row - otherwise two concurrent
        // requests can each observe "another Owner exists" and both commit, leaving zero Owners.
        return await unitOfWork.ExecuteSerializableAsync(async ct =>
        {
            var members = await membershipRepository.ListForTripAsync(tripId, ct).ConfigureAwait(false);
            var target = members.FirstOrDefault(m => m.Id == targetMembershipId)
                ?? throw new NotFoundException("Member not found on this trip.");

            MembershipPolicy.EnsureRoleChangeAllowed(members, target, newRole);

            target.ChangeRole(newRole);
            await membershipRepository.UpdateAsync(target, expectedRowVersion, ct).ConfigureAwait(false);
            return target;
        }, cancellationToken).ConfigureAwait(false);
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

    private Task RemoveCoreAsync(TripId tripId, MembershipId targetMembershipId, CancellationToken cancellationToken) =>
        // See the comment in ChangeRoleAsync: the last-Owner invariant spans every membership row,
        // so the read-check-write here must also be serializable against concurrent
        // removals/demotions targeting other rows on the same trip.
        unitOfWork.ExecuteSerializableAsync(async ct =>
        {
            var members = await membershipRepository.ListForTripAsync(tripId, ct).ConfigureAwait(false);
            var target = members.FirstOrDefault(m => m.Id == targetMembershipId)
                ?? throw new NotFoundException("Member not found on this trip.");

            MembershipPolicy.EnsureRemovalAllowed(members, target);

            await membershipRepository.RemoveAsync(target, ct).ConfigureAwait(false);
        }, cancellationToken);
}
