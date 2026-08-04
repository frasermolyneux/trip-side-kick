using System.Security.Claims;

using MX.TripSideKick.Application.Abstractions;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the claims on the current request's cookie-authenticated
/// principal.
/// </summary>
/// <remarks>
/// The BFF terminates the OpenID Connect authorization-code flow server-side (Microsoft.Identity.Web
/// / <c>AddMicrosoftIdentityWebApp</c> registered in <c>Program.cs</c>); the browser only ever holds
/// the <c>__Host-</c> session cookie, never a token. <see cref="SubjectId"/> is the stable object id
/// (<c>oid</c> claim) that trip membership is keyed on - see docs/identity-and-access.md. Never key
/// authorisation on <see cref="DisplayName"/>: it is mutable and it is PII, and must never be logged
/// or traced.
/// </remarks>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? principal;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        principal = httpContextAccessor.HttpContext?.User;
    }

    public bool IsAuthenticated => principal?.Identity?.IsAuthenticated ?? false;

    public string? SubjectId => IsAuthenticated
        ? principal?.FindFirstValue("oid") ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    public string? DisplayName => IsAuthenticated
        ? principal?.FindFirstValue("name") ?? principal?.Identity?.Name
        : null;

    /// <summary>
    /// Reads the verified email claim. Entra External ID emits this as either <c>email</c> (the
    /// modern v2.0 claim) or, for some external identity providers, <c>emails</c> (a JSON array
    /// claim, of which the first value is used). Falls back to <c>preferred_username</c> only when
    /// it looks like an email address, since some flows put the email there instead.
    /// </summary>
    public string? VerifiedEmail
    {
        get
        {
            if (!IsAuthenticated || principal is null)
            {
                return null;
            }

            var email = principal.FindFirstValue("email")
                ?? principal.FindFirstValue("emails")
                ?? principal.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrWhiteSpace(email))
            {
                return email;
            }

            var preferredUsername = principal.FindFirstValue("preferred_username");
            return preferredUsername is { Length: > 0 } candidate && candidate.Contains('@', StringComparison.Ordinal)
                ? candidate
                : null;
        }
    }
}
