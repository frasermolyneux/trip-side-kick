using MX.TripSideKick.Domain.Invitations;

namespace MX.TripSideKick.Application.Invitations;

/// <summary>
/// Notifies an invitee that they have been invited to plan a trip.
/// </summary>
/// <remarks>
/// Email delivery is not available yet - the platform-notifications <c>tripsidekick.net</c> sending
/// domain is a separate, pending piece of infrastructure. The only implementation registered today
/// is <c>LoggingInvitationNotifier</c>, which just logs that an invitation was created/resent (never
/// the invited email address - PII). The acceptance link itself is returned directly in the
/// invitations API response so invites stay testable end-to-end without email. When
/// platform-notifications lands, a new <c>MX.Platform.Notifications</c>-backed implementation of
/// this interface drops in and is registered instead - no caller of this interface needs to change.
/// </remarks>
public interface IInvitationNotifier
{
    Task NotifyCreatedAsync(Invitation invitation, CancellationToken cancellationToken = default);

    Task NotifyResentAsync(Invitation invitation, CancellationToken cancellationToken = default);
}
