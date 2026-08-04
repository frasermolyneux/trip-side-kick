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
public sealed class AuthController(ICurrentUser currentUser) : ControllerBase
{
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    /// <summary>Challenges the user to sign in via Microsoft Entra External ID.</summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl) =>
        Challenge(
            new AuthenticationProperties { RedirectUri = SafeLocalRedirect(returnUrl) },
            OpenIdConnectDefaults.AuthenticationScheme);

    /// <summary>Signs the user out of both the local session cookie and the identity provider.</summary>
    [HttpGet("logout")]
    public IActionResult Logout([FromQuery] string? returnUrl) =>
        SignOut(
            new AuthenticationProperties { RedirectUri = SafeLocalRedirect(returnUrl) },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);

    /// <summary>Reports the current sign-in state. Never returns tokens.</summary>
    [HttpGet("me")]
    [AllowAnonymous]
    public ActionResult<AuthMeResponse> Me() => Ok(new AuthMeResponse(
        currentUser.IsAuthenticated,
        currentUser.DisplayName));

    // Only ever redirect back into this app - an open redirect would let a phishing link ride the
    // sign-in/sign-out flow to an attacker-controlled destination.
    private string SafeLocalRedirect(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}

/// <summary>Response contract for <c>GET /v1/auth/me</c>.</summary>
/// <param name="IsAuthenticated">Whether the caller has an active signed-in session.</param>
/// <param name="DisplayName">The signed-in user's display name, or <c>null</c> when anonymous. PII - never log this value.</param>
public sealed record AuthMeResponse(bool IsAuthenticated, string? DisplayName);
