using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MX.TripSideKick.Web.Tests.Hosting;

/// <summary>
/// Test-only authentication handler that stands in for the real cookie/OIDC schemes so
/// <c>/v1/auth/me</c> and authorization behaviour can be exercised without a live Entra tenant.
/// </summary>
/// <remarks>
/// Authenticates only when the caller sends <see cref="SubjectHeaderName"/>; otherwise reports
/// <see cref="AuthenticateResult.NoResult"/> so the request is treated as anonymous, exactly like a
/// visitor with no session cookie.
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubjectHeaderName = "X-Test-Subject-Id";
    public const string DisplayNameHeaderName = "X-Test-Display-Name";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubjectHeaderName, out var subjectId) || string.IsNullOrEmpty(subjectId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var displayName = Request.Headers.TryGetValue(DisplayNameHeaderName, out var name)
            ? name.ToString()
            : "Test User";

        var claims = new[]
        {
            new Claim("oid", subjectId!),
            new Claim("name", displayName)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
