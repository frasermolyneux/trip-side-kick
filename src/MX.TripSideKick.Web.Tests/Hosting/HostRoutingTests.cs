using System.Net;

namespace MX.TripSideKick.Web.Tests.Hosting;

/// <summary>
/// Proves the host-aware split: <c>tripsidekick.net</c> serves only the brochure site and
/// <c>tripsidekick.app</c> serves only the PWA shell and the versioned API.
/// </summary>
public sealed class HostRoutingTests(TripSideKickApplicationFactory factory)
    : IClassFixture<TripSideKickApplicationFactory>
{
    private readonly TripSideKickApplicationFactory factory = factory;

    [Fact]
    public async Task Site_host_serves_the_brochure_landing_page()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Plan trips without the spreadsheet", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Site_host_does_not_expose_the_versioned_api()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri("/v1/status", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Site_host_does_not_serve_the_application_shell()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri("/trips/anything", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Site_host_does_not_serve_the_generated_client_bundle()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        // Static files are host-scoped as well as endpoints, so the Vite-generated shell is not
        // reachable on the brochure surface even by requesting the file directly.
        using var response = await client.GetAsync(new Uri("/index.html", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Site_host_serves_its_own_static_assets()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri("/site.css", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task App_host_serves_the_versioned_api()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri("/v1/status", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"authenticated\":false", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/v1/auth/login")]
    [InlineData("/v1/auth/logout")]
    [InlineData("/v1/auth/me")]
    [InlineData("/v1/auth/antiforgery")]
    public async Task App_host_exposes_auth_endpoints(string path)
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1/auth/login")]
    [InlineData("/v1/auth/logout")]
    [InlineData("/v1/auth/me")]
    [InlineData("/v1/auth/antiforgery")]
    public async Task Site_host_does_not_expose_auth_endpoints(string path)
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.SiteHost);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task App_host_serves_the_spa_shell_for_client_routes()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri("/trips/anything", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Plan trips without the spreadsheet", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task App_host_does_not_serve_the_brochure_pages()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri("/Privacy", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", body, StringComparison.Ordinal);
        Assert.DoesNotContain("How Trip Side Kick handles your data", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unrecognised_hosts_are_rejected()
    {
        using var client = factory.CreateClientFor("not-trip-side-kick.example");

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Www_alias_is_redirected_to_the_apex_host()
    {
        using var client = factory.CreateClientFor($"www.{TripSideKickApplicationFactory.SiteHost}");

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.PermanentRedirect, response.StatusCode);
        Assert.Equal(
            $"https://{TripSideKickApplicationFactory.SiteHost}/",
            response.Headers.Location?.ToString());
    }
}
