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
}
