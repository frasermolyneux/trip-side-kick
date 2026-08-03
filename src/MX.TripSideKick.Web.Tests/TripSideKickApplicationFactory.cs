using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
                ["BlobStorage:ServiceUri"] = string.Empty
            }));
    }
}
