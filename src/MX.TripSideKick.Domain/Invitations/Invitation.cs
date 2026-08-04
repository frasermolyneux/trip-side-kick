using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Domain.Invitations;

/// <summary>
/// Invitation aggregate root: Journey 2's "invite a collaborator by email" flow.
/// </summary>
/// <remarks>
/// MVP approach: an app-level pending-membership record claimed via a stubbed acceptance link
/// (<see cref="AcceptanceToken"/>) - no Microsoft Graph B2B <c>/invitations</c> call. Email
/// delivery is not available yet (platform-notifications is a separate pending step), so the
/// acceptance link is surfaced directly in API responses via <c>IInvitationNotifier</c> - see
/// docs/trips-and-membership.md. The invitation is bound to <see cref="InvitedEmail"/> and can only
/// be accepted by a signed-in identity whose verified email matches - see
/// <see cref="EnsureCanBeAcceptedBy"/>.
/// </remarks>
public sealed class Invitation
{
    private Invitation()
    {
    }

    public InvitationId Id { get; private init; }

    public TripId TripId { get; private init; }

    /// <summary>The email the invitation is bound to. Never used for authorization once accepted - only for the acceptance check.</summary>
    public string InvitedEmail { get; private init; } = string.Empty;

    public MembershipRole Role { get; private init; }

    public InvitationStatus Status { get; private set; }

    public TravellerLinkKind LinkKind { get; private init; }

    /// <summary>Set only when <see cref="LinkKind"/> is <see cref="TravellerLinkKind.ExistingTraveller"/>.</summary>
    public TravellerId? ExistingTravellerId { get; private init; }

    /// <summary>Set only when <see cref="LinkKind"/> is <see cref="TravellerLinkKind.NewLinkedTraveller"/>.</summary>
    public string? NewTravellerDisplayName { get; private init; }

    /// <summary>Opaque token embedded in the stubbed acceptance link. Not a secret credential by itself: acceptance still requires signing in as the matching identity.</summary>
    public Guid AcceptanceToken { get; private set; }

    public Instant CreatedAtUtc { get; private init; }

    public Instant? AcceptedAtUtc { get; private set; }

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    public static Invitation Create(
        TripId tripId,
        string invitedEmail,
        MembershipRole role,
        Instant createdAtUtc,
        TravellerLinkKind linkKind = TravellerLinkKind.NonTravellingPlanner,
        TravellerId? existingTravellerId = null,
        string? newTravellerDisplayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitedEmail);
        ValidateLinkKind(linkKind, existingTravellerId, newTravellerDisplayName);

        return new Invitation
        {
            Id = InvitationId.New(),
            TripId = tripId,
            InvitedEmail = invitedEmail.Trim(),
            Role = role,
            Status = InvitationStatus.Pending,
            LinkKind = linkKind,
            ExistingTravellerId = existingTravellerId,
            NewTravellerDisplayName = newTravellerDisplayName,
            AcceptanceToken = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc
        };
    }

    /// <summary>
    /// Regenerates the acceptance token so a previously shared link stops working - used when
    /// resending, in case the original link leaked.
    /// </summary>
    public void Resend()
    {
        EnsurePending("resent");
        AcceptanceToken = Guid.NewGuid();
    }

    public void Revoke()
    {
        EnsurePending("revoked");
        Status = InvitationStatus.Revoked;
    }

    /// <summary>
    /// Throws <see cref="InvitationIdentityMismatchException"/> unless <paramref name="verifiedEmail"/>
    /// matches the email this invitation was bound to. Must be called before creating the
    /// resulting membership - accept is never allowed to be claimed by a different identity.
    /// </summary>
    public void EnsureCanBeAcceptedBy(string? verifiedEmail)
    {
        EnsurePending("accepted");

        if (string.IsNullOrWhiteSpace(verifiedEmail) ||
            !string.Equals(verifiedEmail.Trim(), InvitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvitationIdentityMismatchException(
                "This invitation can only be accepted by signing in with the email address it was sent to.");
        }
    }

    public void MarkAccepted(Instant acceptedAtUtc)
    {
        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = acceptedAtUtc;
    }

    private void EnsurePending(string action)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvitationStateException($"This invitation cannot be {action}: it is {Status}.");
        }
    }

    private static void ValidateLinkKind(TravellerLinkKind linkKind, TravellerId? existingTravellerId, string? newTravellerDisplayName)
    {
        switch (linkKind)
        {
            case TravellerLinkKind.ExistingTraveller when existingTravellerId is null:
                throw new ArgumentException(
                    $"{nameof(existingTravellerId)} is required when {nameof(linkKind)} is {TravellerLinkKind.ExistingTraveller}.",
                    nameof(existingTravellerId));
            case TravellerLinkKind.NewLinkedTraveller when string.IsNullOrWhiteSpace(newTravellerDisplayName):
                throw new ArgumentException(
                    $"{nameof(newTravellerDisplayName)} is required when {nameof(linkKind)} is {TravellerLinkKind.NewLinkedTraveller}.",
                    nameof(newTravellerDisplayName));
            default:
                break;
        }
    }
}
