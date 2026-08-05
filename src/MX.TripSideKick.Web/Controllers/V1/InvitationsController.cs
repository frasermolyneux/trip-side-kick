using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;
using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// Journey 2's invitation flow: invite by email + role, pending/resend/revoke (all Owner-only,
/// nested under the trip), and accepting an invitation by its acceptance token (any signed-in
/// user - that is how they get their first membership on a trip). App hosts only.
/// </summary>
/// <remarks>
/// Email delivery is not available yet - <see cref="InvitationResponse.AcceptanceUrl"/> is the
/// stubbed link a real notifier would eventually email; it is returned directly here so invites
/// stay testable end-to-end without email (see <see cref="IInvitationNotifier"/>).
/// </remarks>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class InvitationsController(InvitationService invitationService, ICurrentUser currentUser) : ControllerBase
{
    private readonly InvitationService invitationService = invitationService
        ?? throw new ArgumentNullException(nameof(invitationService));
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    /// <summary>Lists invitations (pending, accepted and revoked) for a trip. Owner-only.</summary>
    [HttpGet("v1/trips/{tripId:guid}/invitations")]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> List(Guid tripId, CancellationToken cancellationToken)
    {
        var invitations = await invitationService
            .ListInvitationsAsync(new TripId(tripId), RequireSubjectId(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(invitations.Select(ToResponse).ToList());
    }

    /// <summary>Creates a pending invitation. Owner-only.</summary>
    [HttpPost("v1/trips/{tripId:guid}/invitations")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<InvitationResponse>> Create(
        Guid tripId, [FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invitation = await invitationService.CreateInvitationAsync(
            new TripId(tripId),
            RequireSubjectId(),
            new CreateInvitationInput(
                request.InvitedEmail,
                request.Role,
                request.LinkKind,
                request.ExistingTravellerId is { } id ? new TravellerId(id) : null,
                request.NewTravellerDisplayName),
            cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(List), new { tripId }, ToResponse(invitation));
    }

    /// <summary>Regenerates the acceptance link for a pending invitation (invalidating any previously shared link). Owner-only.</summary>
    [HttpPost("v1/trips/{tripId:guid}/invitations/{invitationId:guid}/resend")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<InvitationResponse>> Resend(Guid tripId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await invitationService.ResendInvitationAsync(
            new TripId(tripId), new InvitationId(invitationId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);

        return Ok(ToResponse(invitation));
    }

    /// <summary>Revokes a pending invitation. Owner-only.</summary>
    [HttpPost("v1/trips/{tripId:guid}/invitations/{invitationId:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid tripId, Guid invitationId, CancellationToken cancellationToken)
    {
        await invitationService.RevokeInvitationAsync(
            new TripId(tripId), new InvitationId(invitationId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Accepts an invitation by its acceptance token. Only succeeds when the signed-in user's
    /// Entra-verified email matches the invited email exactly - see
    /// <see cref="Invitation.EnsureCanBeAcceptedBy"/>.
    /// </summary>
    [HttpPost("v1/invitations/accept")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<MembershipResponse>> Accept(
        [FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var membership = await invitationService.AcceptInvitationAsync(
            request.AcceptanceToken, RequireSubjectId(), currentUser.VerifiedEmail, cancellationToken).ConfigureAwait(false);

        return Ok(MembershipResponse.From(membership));
    }

    private InvitationResponse ToResponse(Invitation invitation) => InvitationResponse.From(invitation, BuildAcceptanceUrl(invitation.AcceptanceToken));

    // The acceptance link points at the React client's own accept-invitation route, which parses
    // the token from the query string and (after ensuring the visitor is signed in) calls
    // POST /v1/invitations/accept. Built from the current request so it always targets whichever
    // app host served this request (tripsidekick.app / dev.tripsidekick.app).
    private string BuildAcceptanceUrl(Guid acceptanceToken) =>
        $"{Request.Scheme}://{Request.Host}/invitations/accept?token={acceptanceToken}";

    private string RequireSubjectId() =>
        currentUser.SubjectId ?? throw new InvalidOperationException("An authenticated request must have a subject id.");
}

/// <summary>Request body for <c>POST /v1/trips/{tripId}/invitations</c>.</summary>
public sealed record CreateInvitationRequest(
    string InvitedEmail,
    MembershipRole Role,
    TravellerLinkKind LinkKind,
    Guid? ExistingTravellerId,
    string? NewTravellerDisplayName);

/// <summary>Request body for <c>POST /v1/invitations/accept</c>.</summary>
public sealed record AcceptInvitationRequest(Guid AcceptanceToken);

/// <summary>Response contract for an invitation. <see cref="InvitedEmail"/> and <see cref="AcceptanceUrl"/> are PII/security-sensitive - never log them.</summary>
public sealed record InvitationResponse(
    Guid Id,
    Guid TripId,
    string InvitedEmail,
    MembershipRole Role,
    string Status,
    TravellerLinkKind LinkKind,
    string AcceptanceUrl)
{
    public static InvitationResponse From(Invitation invitation, string acceptanceUrl) => new(
        invitation.Id.Value,
        invitation.TripId.Value,
        invitation.InvitedEmail,
        invitation.Role,
        invitation.Status.ToString().ToLowerInvariant(),
        invitation.LinkKind,
        acceptanceUrl);
}
