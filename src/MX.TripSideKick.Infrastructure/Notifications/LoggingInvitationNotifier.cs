using Microsoft.Extensions.Logging;

using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Domain.Invitations;

namespace MX.TripSideKick.Infrastructure.Notifications;

/// <summary>
/// Placeholder <see cref="IInvitationNotifier"/> that only logs that an invitation event
/// occurred. Email delivery is not available yet (the platform-notifications
/// <c>tripsidekick.net</c> sending domain is a separate, pending piece of infrastructure) - the
/// acceptance link is returned directly in the invitations API response so invites remain
/// testable without email. Swap this out for an <c>MX.Platform.Notifications</c>-backed
/// implementation once that lands; no caller of <see cref="IInvitationNotifier"/> needs to change.
/// </summary>
/// <remarks>
/// Deliberately never logs the invited email address or display names - both are PII.
/// </remarks>
public sealed partial class LoggingInvitationNotifier(ILogger<LoggingInvitationNotifier> logger) : IInvitationNotifier
{
    private readonly ILogger<LoggingInvitationNotifier> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task NotifyCreatedAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        LogInvitationCreated(invitation.Id.Value, invitation.TripId.Value);
        return Task.CompletedTask;
    }

    public Task NotifyResentAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        LogInvitationResent(invitation.Id.Value, invitation.TripId.Value);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Invitation {InvitationId} created for trip {TripId}. Email delivery is not configured; the acceptance link must be surfaced by the caller.")]
    private partial void LogInvitationCreated(Guid invitationId, Guid tripId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Invitation {InvitationId} resent for trip {TripId}. Email delivery is not configured; the acceptance link must be surfaced by the caller.")]
    private partial void LogInvitationResent(Guid invitationId, Guid tripId);
}
