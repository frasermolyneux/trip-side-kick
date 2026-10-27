using System.Net;

namespace MX.TripSideKick.Web.Tests.Health;

/// <summary>
/// Only <c>/api/health/live</c> and <c>/api/health/ready</c> are exposed, per
/// <c>standards.health-endpoints</c>. Readiness requires PostgreSQL; liveness does not.
/// </summary>
public sealed class HealthEndpointTests(TripSideKickApplicationFactory factory)
    : IClassFixture<TripSideKickApplicationFactory>
{
    private readonly TripSideKickApplicationFactory factory = factory;

    [Theory]
    [InlineData("/api/health/live", HttpStatusCode.OK, "Healthy")]
    [InlineData("/api/health/ready", HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task Health_endpoints_fail_readiness_without_a_database(string path, HttpStatusCode status, string expected)
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(expected, body);
    }

    [Fact]
    public async Task Health_endpoints_are_reachable_from_the_brochure_host()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri("/api/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/healthz")]
    [InlineData("/api/health")]
    public async Task Legacy_health_aliases_are_not_exposed(string path)
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Info_endpoint_reports_the_build_version()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri("/info", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"buildVersion\"", body, StringComparison.Ordinal);
    }
}
