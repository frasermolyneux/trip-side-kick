using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// BFF sign-in endpoints for the app surface. Terminates the OpenID Connect authorization-code
/// flow server-side: the browser only ever sees the <c>__Host-</c> session cookie, never a token.
/// </summary>
/// <remarks>
/// Reachable only on the app hosts - this controller is mapped by the same
/// <c>MapControllers().RequireHost(appHosts)</c> call as every other v1 controller (see
/// <c>Program.cs</c>), so the brochure surface never exposes a sign-in surface, per
/// docs/identity-and-access.md.
/// </remarks>
[ApiController]
[Route("v1/auth")]
[Produces("application/json")]
public sealed class AuthController(ICurrentUser currentUser, IAntiforgery antiforgery) : ControllerBase
{
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    private readonly IAntiforgery antiforgery = antiforgery ?? throw new ArgumentNullException(nameof(antiforgery));

    /// <summary>Challenges the user to sign in via Microsoft Entra External ID.</summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl) =>
        Challenge(
            new AuthenticationProperties { RedirectUri = SafeLocalRedirect(returnUrl) },
            OpenIdConnectDefaults.AuthenticationScheme);

    /// <summary>
    /// Signs the user out of both the local session cookie and the identity provider. A POST
    /// guarded by the antiforgery token (rather than a plain <c>GET</c>) because <c>SameSite=Lax</c>
    /// cookies are still attached to top-level cross-site GET navigations - a bare GET endpoint
    /// would let any third-party page force-log a signed-in user out simply by linking to it.
    /// </summary>
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout([FromQuery] string? returnUrl) =>
        SignOut(
            new AuthenticationProperties { RedirectUri = SafeLocalRedirect(returnUrl) },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);

    /// <summary>
    /// Issues an antiforgery token pair for the SPA: the cookie half is set automatically (and is
    /// <c>HttpOnly</c>, so JavaScript never reads it), and the request-token half is returned in the
    /// response body for the SPA to echo back in the <c>X-CSRF-TOKEN</c> header on <c>POST /v1/auth/logout</c>.
    /// </summary>
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public IActionResult GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }

    /// <summary>Reports the current sign-in state. Never returns tokens.</summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public ActionResult<AuthMeResponse> Me() => Ok(new AuthMeResponse(
        currentUser.IsAuthenticated,
        currentUser.DisplayName,
        currentUser.SubjectId));

    // Only ever redirect back into this app - an open redirect would let a phishing link ride the
    // sign-in/sign-out flow to an attacker-controlled destination.
    private string SafeLocalRedirect(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}

/// <summary>Response contract for <c>GET /v1/auth/me</c>.</summary>
/// <param name="IsAuthenticated">Whether the caller has an active signed-in session.</param>
/// <param name="DisplayName">The signed-in user's display name, or <c>null</c> when anonymous. PII - never log this value.</param>
/// <param name="SubjectId">
/// The signed-in user's opaque subject id (Entra <c>oid</c>), or <c>null</c> when anonymous. Not
/// PII - it is the same stable, non-reassignable identifier authorization is keyed on
/// server-side (see docs/identity-and-access.md) and lets the client determine "is this me"
/// (e.g. against a <c>MembershipResponse.SubjectId</c>) without ever comparing on email/name.
/// </param>
public sealed record AuthMeResponse(bool IsAuthenticated, string? DisplayName, string? SubjectId);

/// <summary>Response contract for <c>GET /v1/auth/antiforgery</c>.</summary>
/// <param name="Token">The request-token value to echo back in the <c>X-CSRF-TOKEN</c> header on state-changing calls.</param>
public sealed record AntiforgeryTokenResponse(string Token);
