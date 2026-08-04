using System.Net;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MX.TripSideKick.Web.Hosting;

namespace MX.TripSideKick.Web.Tests.Hosting;

/// <summary>
/// Proves the Playwright/E2E-only <see cref="TestAuthEndpoints"/> sign-in endpoint is fail-closed:
/// unreachable unless both the ASP.NET Core environment is Development <em>and</em>
/// <c>TestAuth:Enabled</c> is explicitly <c>true</c>. This is the security-sensitive control the
/// spec calls out - see docs/testing.md.
/// </summary>
public sealed class TestAuthEndpointsTests : IClassFixture<TripSideKickApplicationFactory>
{
    private readonly TripSideKickApplicationFactory factory;

    public TestAuthEndpointsTests(TripSideKickApplicationFactory factory)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    [Fact]
    public async Task Signin_endpoint_is_unreachable_when_TestAuth_Enabled_is_not_set()
    {
        // The shared factory runs in Development (see TripSideKickApplicationFactory) but never
        // sets TestAuth:Enabled - this is the out-of-the-box configuration every deployed
        // environment actually has. Since the app-host's SPA fallback route matches any unmapped
        // path, the request resolves to index.html (200) rather than a literal 404 - the property
        // that actually matters is that no sign-in occurred: no Set-Cookie header is emitted, and a
        // follow-up call proves the caller is still anonymous.
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        var response = await client.GetAsync(new Uri($"{TestAuthEndpoints.SignInPath}?sub=test-subject", UriKind.Relative));

        Assert.False(response.Headers.Contains("Set-Cookie"));

        var meResponse = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));
        var me = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"isAuthenticated\":false", me, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signin_endpoint_is_unreachable_outside_Development_even_when_enabled()
    {
        using var productionFactory = factory.WithWebHostBuilder(builder =>
        {
            // Deployed environments run ASPNETCORE_ENVIRONMENT=Production - proving the endpoint
            // stays unmapped there even if TestAuth:Enabled were somehow set is the point of this
            // test: two independent conditions must both hold, not just one.
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["TestAuth:Enabled"] = "true" }));
        });

        using var client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{TripSideKickApplicationFactory.AppHost}/")
        });

        var response = await client.GetAsync(new Uri($"{TestAuthEndpoints.SignInPath}?sub=test-subject", UriKind.Relative));

        Assert.False(response.Headers.Contains("Set-Cookie"));

        var meResponse = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));
        var me = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"isAuthenticated\":false", me, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signin_endpoint_authenticates_the_caller_when_explicitly_enabled_in_Development()
    {
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["TestAuth:Enabled"] = "true" }));

            // The shared factory's own ConfigureWebHost swaps the DEFAULT authentication scheme to
            // TestAuthHandler (header-based) for the benefit of integration tests. That would defeat
            // this test, which exercises the real cookie-based sign-in a browser gets from
            // /testauth/signin - so restore Cookies as the default scheme here, exactly as
            // Program.cs configures it in every real environment.
            builder.ConfigureTestServices(services => services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme));
        });

        using var client = enabledFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{TripSideKickApplicationFactory.AppHost}/")
        });

        var signInResponse = await client.GetAsync(
            new Uri($"{TestAuthEndpoints.SignInPath}?sub=e2e-subject-1&email=e2e%40example.test&name=E2E%20User", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);

        var meResponse = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));
        var me = await meResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Contains("\"isAuthenticated\":true", me, StringComparison.OrdinalIgnoreCase);
    }
}
