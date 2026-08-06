namespace MX.TripSideKick.Domain.Common;

/// <summary>
/// Base type for exceptions that represent a violated domain invariant (as opposed to
/// infrastructure failures). Application/Web layers translate these into 4xx responses.
/// </summary>
public abstract class DomainRuleViolationException(string message) : Exception(message);

/// <summary>Thrown when an operation would leave a trip without any Owner.</summary>
public sealed class LastOwnerViolationException(string message) : DomainRuleViolationException(message);

/// <summary>Thrown when an invitation is accepted by an identity that does not match the invited email.</summary>
public sealed class InvitationIdentityMismatchException(string message) : DomainRuleViolationException(message);

/// <summary>Thrown when an invitation cannot be accepted/resent/revoked because of its current status.</summary>
public sealed class InvitationStateException(string message) : DomainRuleViolationException(message);

/// <summary>
/// Thrown when an itinerary item cannot be scheduled onto a day because the trip's dates are not
/// confirmed yet (day-by-day scheduling requires <c>TripDateStatus.Confirmed</c>).
/// </summary>
public sealed class SchedulingNotSupportedException(string message) : DomainRuleViolationException(message);
