namespace MX.TripSideKick.Application.Common;

/// <summary>
/// The caller asked for a resource that either doesn't exist or that they have no membership on.
/// Deliberately used for "not a member of this trip" too (rather than
/// <see cref="ForbiddenException"/>) so non-members can't distinguish "doesn't exist" from
/// "exists but you're not on it" - see docs/identity-and-access.md.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The caller is a member of the trip but their role doesn't permit this action.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>
/// An <c>If-Match</c> ETag (backed by SQL <c>rowversion</c>) didn't match the current value -
/// someone else changed the resource first.
/// </summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);

/// <summary>The signed-in user already has a membership on this trip.</summary>
public sealed class AlreadyMemberException(string message) : Exception(message);
