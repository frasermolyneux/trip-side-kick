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
/// Never enable test authentication on a public host. The App Service instance check
/// is an additional legacy safeguard, not a way to detect every deployed environment.
/// When any condition fails, no sign-in endpoint is mapped and no cookie is issued.
/// Production is excluded regardless of whether <c>WEBSITE_INSTANCE_ID</c> is set.
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
