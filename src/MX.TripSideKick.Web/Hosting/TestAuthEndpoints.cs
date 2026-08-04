using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

using MX.TripSideKick.Web.Options;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// A deterministic, no-Entra sign-in endpoint that exists purely so hermetic Playwright/E2E tests
/// can drive the real built app as an arbitrary identity/role without a live Entra tenant.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Security-sensitive control - fail-closed by construction.</strong> The endpoint is only
/// mapped when <em>both</em> of the following hold:
/// </para>
/// <list type="bullet">
/// <item>the ASP.NET Core hosting environment is <c>Development</c> (<see cref="IWebHostEnvironment.IsDevelopment"/>), and</item>
/// <item><c>TestAuth:Enabled</c> is explicitly <c>true</c> (see <see cref="TestAuthOptions"/>).</item>
/// </list>
/// <para>
/// Neither condition is ever true in a deployed environment: <c>ASPNETCORE_ENVIRONMENT</c> is
/// <c>Production</c> for both <c>dev</c> and <c>prd</c> App Services (see
/// <c>terraform/web_app.tf</c> - "dev" here means the pre-production deployment slot, not the
/// ASP.NET Core Development environment), and <c>TestAuth__Enabled</c> is never set by Terraform.
/// This is proved by
/// <c>MX.TripSideKick.Web.Tests.Hosting.TestAuthEndpointsTests</c>: when either condition doesn't
/// hold, this method never maps the endpoint, so the request falls through to the app's SPA
/// fallback route (<c>index.html</c>, HTTP 200) rather than the sign-in handler above - the
/// fail-closed guarantee is that <em>no cookie is issued and the caller stays anonymous</em>
/// (<c>/v1/auth/me</c> reports <c>isAuthenticated: false</c>), not a literal 404.
/// </para>
/// <para>
/// On success, it signs the caller in via the <em>same</em> cookie authentication scheme
/// production uses (<see cref="CookieAuthenticationDefaults.AuthenticationScheme"/>), with the
/// same claim shapes <c>HttpContextCurrentUser</c> reads (<c>oid</c>, <c>name</c>, <c>email</c>) -
/// so everything downstream (authorization, <c>ICurrentUser</c>, invitation-acceptance email
/// matching) behaves exactly as it would for a real signed-in user.
/// </para>
/// </remarks>
public static class TestAuthEndpoints
{
    public const string SignInPath = "/testauth/signin";
    public const string SignOutPath = "/testauth/signout";

    public static IEndpointRouteBuilder MapTestAuthEndpoints(
        this IEndpointRouteBuilder endpoints,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        params string[] hosts)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        var enabled = configuration.GetSection(TestAuthOptions.SectionName).Get<TestAuthOptions>()?.Enabled ?? false;
        if (!enabled)
        {
            return endpoints;
        }

        endpoints.MapGet(SignInPath, async (
            HttpContext httpContext, string sub, string? email, string? name) =>
        {
            if (string.IsNullOrWhiteSpace(sub))
            {
                return Results.BadRequest("The 'sub' query parameter is required.");
            }

            var claims = new List<Claim>
            {
                new("oid", sub),
                new("name", string.IsNullOrWhiteSpace(name) ? "E2E Test User" : name)
            };

            if (!string.IsNullOrWhiteSpace(email))
            {
                claims.Add(new Claim("email", email));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal)
                .ConfigureAwait(false);

            return Results.Ok(new { signedIn = true, sub });
        }).RequireHost(hosts);

        endpoints.MapPost(SignOutPath, async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return Results.Ok(new { signedIn = false });
        }).RequireHost(hosts);

        return endpoints;
    }
}
