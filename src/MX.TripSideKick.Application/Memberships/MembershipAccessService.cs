using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Memberships;

/// <summary>
/// Shared "does this subject have at least this role on this trip" check used by every
/// trip-scoped application service, so the role matrix (Owner/Editor/Viewer) is enforced
/// consistently in one place.
/// </summary>
public sealed class MembershipAccessService(IMembershipRepository membershipRepository)
{
    private readonly IMembershipRepository membershipRepository = membershipRepository
        ?? throw new ArgumentNullException(nameof(membershipRepository));

    /// <summary>
    /// Returns the caller's membership on the trip, or throws <see cref="NotFoundException"/> if
    /// they have none (never <see cref="ForbiddenException"/> for "not a member" - see
    /// <see cref="NotFoundException"/>'s remarks) or <see cref="ForbiddenException"/> if their role
    /// is below <paramref name="minimumRole"/>.
    /// </summary>
    public async Task<Membership> RequireRoleAsync(
        TripId tripId, string subjectId, MembershipRole minimumRole, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var membership = await membershipRepository
            .GetForTripAndSubjectAsync(tripId, subjectId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            throw new NotFoundException("Trip not found.");
        }

        if (membership.Role < minimumRole)
        {
            throw new ForbiddenException(
                $"This action requires the {minimumRole} role or higher; you are a {membership.Role}.");
        }

        return membership;
    }
}
