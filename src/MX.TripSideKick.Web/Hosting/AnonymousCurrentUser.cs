using MX.TripSideKick.Application.Abstractions;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// IDENTITY STUB — always reports an anonymous user.
/// </summary>
/// <remarks>
/// <para>
/// The walking skeleton ships with authentication deliberately switched off: no Entra External ID
/// application registration, no user flow, no sign-in/sign-out routes, and no protected endpoints.
/// </para>
/// <para>
/// TODO (identity slice): replace this with an <see cref="ICurrentUser"/> implementation backed by
/// <c>IHttpContextAccessor</c> claims once Microsoft Entra External ID (B2B collaboration plus
/// self-service sign-up with email OTP and personal Microsoft accounts) is wired up in the
/// Molyneux.IO workforce tenant. See <c>docs/identity-and-access.md</c>.
/// </para>
/// </remarks>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public string? SubjectId => null;

    public string? DisplayName => null;
}
