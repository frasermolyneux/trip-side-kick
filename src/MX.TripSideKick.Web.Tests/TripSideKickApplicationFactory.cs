using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using MX.TripSideKick.Web.Tests.Hosting;

namespace MX.TripSideKick.Web.Tests;

/// <summary>
/// Boots the real application pipeline with deterministic host-routing configuration so the
/// <c>.net</c> / <c>.app</c> split can be asserted without depending on production hostnames.
/// </summary>
public sealed class TripSideKickApplicationFactory : WebApplicationFactory<Program>
{
    public const string SiteHost = "tripsidekick.test";
    public const string AppHost = "app.tripsidekick.test";

    public HttpClient CreateClientFor(string host, bool allowAutoRedirect = false) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            BaseAddress = new Uri($"https://{host}/")
        });

    /// <summary>
    /// An app-host client that authenticates as a fake user via <see cref="TestAuthHandler"/> - no
    /// real Entra tenant, cookie, or token is involved.
    /// </summary>
    public HttpClient CreateAuthenticatedClientFor(
        string host, string subjectId, string displayName = "Test User", string? email = null)
    {
        var client = CreateClientFor(host);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeaderName, subjectId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.DisplayNameHeaderName, displayName);
        if (email is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeaderName, email);
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["HostRouting:SiteHosts:0"] = SiteHost,
                ["HostRouting:AppHosts:0"] = AppHost,
                ["HostRouting:RedirectWwwToApex"] = "true",
                ["ApplicationInsights:ClientConnectionString"] = string.Empty,
                ["BlobStorage:ServiceUri"] = string.Empty,

                // Deterministic, non-secret placeholders so Microsoft.Identity.Web can bind its
                // options at startup; no real Entra tenant is ever contacted in tests.
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "00000000-0000-0000-0000-000000000000",
                ["AzureAd:ClientId"] = "11111111-1111-1111-1111-111111111111",
                ["AzureAd:CallbackPath"] = "/signin-oidc",
                ["AzureAd:SignedOutCallbackPath"] = "/signout-callback-oidc"
            }));

        // Swaps the default authenticate scheme for the test double above: requests with no
        // X-Test-Subject-Id header are anonymous, exactly like a visitor with no session cookie.
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Microsoft.Identity.Web resolves the OIDC metadata document from the real
            // login.microsoftonline.com endpoint by default. Tests never sign in for real, but
            // GET /v1/auth/login still calls ChallengeAsync, which would otherwise make a live
            // network call against the placeholder tenant id above. Pre-seeding the static
            // configuration keeps the test suite fully offline.
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0",
                    AuthorizationEndpoint = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/oauth2/v2.0/authorize",
                    TokenEndpoint = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/oauth2/v2.0/token",
                    JwksUri = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/discovery/v2.0/keys",
                    EndSessionEndpoint = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/oauth2/v2.0/logout"
                });
        });
    }
}

