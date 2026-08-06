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
/// mapped when <em>all</em> of the following hold:
/// </para>
/// <list type="bullet">
/// <item>the ASP.NET Core hosting environment is <c>Development</c> (<see cref="IWebHostEnvironment.IsDevelopment"/>),</item>
/// <item><c>TestAuth:Enabled</c> is explicitly <c>true</c> (see <see cref="TestAuthOptions"/>), and</item>
/// <item>the process is <em>not</em> running as an Azure App Service instance (the
/// <c>WEBSITE_INSTANCE_ID</c> app setting, which Azure App Service always injects and which
/// Terraform never sets and cannot be unset while hosted there, is absent).</item>
/// </list>
/// <para>
/// The third gate exists because <c>ASPNETCORE_ENVIRONMENT=Development</c> is <em>not</em> a
/// reliable deployed-environment signal on its own: <c>terraform/web_app.tf</c> sets
/// <c>ASPNETCORE_ENVIRONMENT</c> to <c>Development</c> (not <c>Production</c>) for the <c>dev</c>
/// App Service slot - "dev" there means the pre-production deployment environment, not the
/// ASP.NET Core hosting environment name. That makes <see cref="IWebHostEnvironment.IsDevelopment"/>
/// true on a real, internet-reachable, custom-domain-bound App Service, so it is only the
/// <c>TestAuth:Enabled</c> flag (never set by Terraform for either <c>dev</c> or <c>prd</c>) standing
/// between that slot and an authentication bypass. The <c>WEBSITE_INSTANCE_ID</c> check restores a
/// second, genuinely independent barrier: it is always present when the app is hosted in Azure App
/// Service (dev or prd) regardless of <c>ASPNETCORE_ENVIRONMENT</c>, and always absent for a local
/// developer run, CI job, or the Playwright harness's spawned child process. This is proved by
/// <c>MX.TripSideKick.Web.Tests.Hosting.TestAuthEndpointsTests</c>: when any condition doesn't hold,
/// this method never maps the endpoint, so the request falls through to the app's SPA fallback
/// route (<c>index.html</c>, HTTP 200) rather than the sign-in handler above - the fail-closed
/// guarantee is that <em>no cookie is issued and the caller stays anonymous</em> (<c>/v1/auth/me</c>
/// reports <c>isAuthenticated: false</c>), not a literal 404.
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

    /// <summary>
    /// App setting Azure App Service always injects into every instance's process environment,
    /// regardless of <c>ASPNETCORE_ENVIRONMENT</c>. Terraform never sets it (it isn't a concept
    /// outside of App Service), so its presence is a reliable "this is a real deployed App Service
    /// instance" signal independent of the Development/Production environment name.
    /// </summary>
    private const string AppServiceInstanceIdSetting = "WEBSITE_INSTANCE_ID";

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

        if (!string.IsNullOrEmpty(configuration[AppServiceInstanceIdSetting]))
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
