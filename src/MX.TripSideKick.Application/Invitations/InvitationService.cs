using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Application.Invitations;

/// <summary>Input for <see cref="InvitationService.CreateInvitationAsync"/>.</summary>
public sealed record CreateInvitationInput(
    string InvitedEmail,
    MembershipRole Role,
    TravellerLinkKind LinkKind = TravellerLinkKind.NonTravellingPlanner,
    TravellerId? ExistingTravellerId = null,
    string? NewTravellerDisplayName = null);

/// <summary>
/// Application service for Journey 2's invitation flow: invite by email + role, optionally linking
/// to a traveller; pending/resend/revoke; accept bound to the invited email.
/// </summary>
/// <remarks>
/// MVP approach: app-level pending-membership records claimed via a stubbed acceptance link - no
/// Microsoft Graph B2B <c>/invitations</c> call (a future enhancement if ever needed). Email
/// delivery is not available yet; see <see cref="IInvitationNotifier"/>.
/// </remarks>
public sealed class InvitationService(
    IInvitationRepository invitationRepository,
    IMembershipRepository membershipRepository,
    ITravellerRepository travellerRepository,
    MembershipAccessService membershipAccess,
    IInvitationNotifier notifier,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private readonly IInvitationRepository invitationRepository = invitationRepository
        ?? throw new ArgumentNullException(nameof(invitationRepository));
    private readonly IMembershipRepository membershipRepository = membershipRepository
        ?? throw new ArgumentNullException(nameof(membershipRepository));
    private readonly ITravellerRepository travellerRepository = travellerRepository
        ?? throw new ArgumentNullException(nameof(travellerRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess
        ?? throw new ArgumentNullException(nameof(membershipAccess));
    private readonly IInvitationNotifier notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    private readonly IUnitOfWork unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Lists invitations (pending, accepted and revoked). Owner-only.</summary>
    public async Task<IReadOnlyList<Invitation>> ListInvitationsAsync(
        TripId tripId, string actingSubjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, actingSubjectId, MembershipRole.Owner, cancellationToken).ConfigureAwait(false);
        return await invitationRepository.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a pending invitation. Owner-only.</summary>
    public async Task<Invitation> CreateInvitationAsync(
        TripId tripId, string actingSubjectId, CreateInvitationInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await membershipAccess.RequireRoleAsync(tripId, actingSubjectId, MembershipRole.Owner, cancellationToken).ConfigureAwait(false);

        if (input.LinkKind == TravellerLinkKind.ExistingTraveller && input.ExistingTravellerId is { } existingId)
        {
            var traveller = await travellerRepository.GetAsync(existingId, cancellationToken).ConfigureAwait(false);
            if (traveller is null || traveller.TripId != tripId)
            {
                throw new NotFoundException("Traveller not found on this trip.");
            }

            if (traveller.LinkedMembershipId is not null)
            {
                throw new AlreadyMemberException("That traveller is already linked to a member.");
            }
        }

        var invitation = Invitation.Create(
            tripId,
            input.InvitedEmail,
            input.Role,
            clock.GetCurrentInstant(),
            input.LinkKind,
            input.ExistingTravellerId,
            input.NewTravellerDisplayName);

        await invitationRepository.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await notifier.NotifyCreatedAsync(invitation, cancellationToken).ConfigureAwait(false);
        return invitation;
    }

    /// <summary>Regenerates the acceptance link for a pending invitation. Owner-only.</summary>
    public async Task<Invitation> ResendInvitationAsync(
        TripId tripId, InvitationId invitationId, string actingSubjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, actingSubjectId, MembershipRole.Owner, cancellationToken).ConfigureAwait(false);

        var invitation = await GetOwnedInvitationAsync(tripId, invitationId, cancellationToken).ConfigureAwait(false);
        var rowVersion = invitation.RowVersion ?? [];
        invitation.Resend();
        await invitationRepository.UpdateAsync(invitation, rowVersion, cancellationToken).ConfigureAwait(false);
        await notifier.NotifyResentAsync(invitation, cancellationToken).ConfigureAwait(false);
        return invitation;
    }

    /// <summary>Revokes a pending invitation. Owner-only.</summary>
    public async Task RevokeInvitationAsync(
        TripId tripId, InvitationId invitationId, string actingSubjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, actingSubjectId, MembershipRole.Owner, cancellationToken).ConfigureAwait(false);

        var invitation = await GetOwnedInvitationAsync(tripId, invitationId, cancellationToken).ConfigureAwait(false);
        var rowVersion = invitation.RowVersion ?? [];
        invitation.Revoke();
        await invitationRepository.UpdateAsync(invitation, rowVersion, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Accepts an invitation by its acceptance token. Only succeeds when
    /// <paramref name="verifiedEmail"/> (the signed-in user's Entra-verified email) matches the
    /// invited email exactly - an invite can never be claimed by a different identity. Creates the
    /// resulting membership and, depending on <see cref="Invitation.LinkKind"/>, links an existing
    /// traveller or creates a new one - never a duplicate traveller record.
    /// </summary>
    public Task<Membership> AcceptInvitationAsync(
        Guid acceptanceToken, string acceptingSubjectId, string? verifiedEmail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptingSubjectId);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            var invitation = await invitationRepository.GetByAcceptanceTokenAsync(acceptanceToken, ct).ConfigureAwait(false)
                ?? throw new NotFoundException("Invitation not found.");

            invitation.EnsureCanBeAcceptedBy(verifiedEmail);

            var existingMembership = await membershipRepository
                .GetForTripAndSubjectAsync(invitation.TripId, acceptingSubjectId, ct)
                .ConfigureAwait(false);

            if (existingMembership is not null)
            {
                throw new AlreadyMemberException("You are already a member of this trip.");
            }

            var membership = Membership.Create(invitation.TripId, acceptingSubjectId, invitation.Role);
            await membershipRepository.AddAsync(membership, ct).ConfigureAwait(false);

            switch (invitation.LinkKind)
            {
                case TravellerLinkKind.ExistingTraveller when invitation.ExistingTravellerId is { } travellerId:
                    var existingTraveller = await travellerRepository.GetAsync(travellerId, ct).ConfigureAwait(false)
                        ?? throw new NotFoundException("Linked traveller no longer exists.");
                    if (existingTraveller.LinkedMembershipId is not null)
                    {
                        throw new AlreadyMemberException("That traveller is already linked to a member.");
                    }

                    existingTraveller.LinkToMembership(membership.Id);
                    await travellerRepository
                        .UpdateAsync(existingTraveller, existingTraveller.RowVersion ?? [], ct)
                        .ConfigureAwait(false);
                    break;

                case TravellerLinkKind.NewLinkedTraveller when invitation.NewTravellerDisplayName is { } displayName:
                    var newTraveller = Traveller.Create(invitation.TripId, displayName, membership.Id);
                    await travellerRepository.AddAsync(newTraveller, ct).ConfigureAwait(false);
                    break;

                case TravellerLinkKind.NonTravellingPlanner:
                default:
                    break;
            }

            invitation.MarkAccepted(clock.GetCurrentInstant());
            await invitationRepository.UpdateAsync(invitation, invitation.RowVersion ?? [], ct).ConfigureAwait(false);

            return membership;
        }, cancellationToken);
    }

    private async Task<Invitation> GetOwnedInvitationAsync(TripId tripId, InvitationId invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitationRepository.GetAsync(invitationId, cancellationToken).ConfigureAwait(false);
        if (invitation is null || invitation.TripId != tripId)
        {
            throw new NotFoundException("Invitation not found on this trip.");
        }

        return invitation;
    }
}
