using Microsoft.Extensions.Options;

using Moq;

using MX.TripSideKick.Web.Hosting;

namespace MX.TripSideKick.Web.Tests.Hosting;

public sealed class HostSurfaceResolverTests
{
    private static HostSurfaceResolver CreateResolver(bool redirectWwwToApex = true)
    {
        var options = new HostRoutingOptions { RedirectWwwToApex = redirectWwwToApex };
        options.SiteHosts.Add("tripsidekick.net");
        options.AppHosts.Add("tripsidekick.app");

        var monitor = new Mock<IOptionsMonitor<HostRoutingOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(options);

        return new HostSurfaceResolver(monitor.Object);
    }

    [Theory]
    [InlineData("tripsidekick.net", HostSurface.Site)]
    [InlineData("TripSideKick.NET", HostSurface.Site)]
    [InlineData("tripsidekick.app", HostSurface.App)]
    [InlineData("tripsidekick.com", HostSurface.Unknown)]
    [InlineData("", HostSurface.Unknown)]
    public void Resolve_maps_hosts_to_surfaces(string host, HostSurface expected) =>
        Assert.Equal(expected, CreateResolver().Resolve(host));

    [Fact]
    public void Www_alias_of_a_known_host_is_redirected()
    {
        var resolved = CreateResolver().TryGetApexRedirectTarget("www.tripsidekick.app", out var apex);

        Assert.True(resolved);
        Assert.Equal("tripsidekick.app", apex);
    }

    [Fact]
    public void Www_alias_of_an_unknown_host_is_not_redirected() =>
        Assert.False(CreateResolver().TryGetApexRedirectTarget("www.example.com", out _));

    [Fact]
    public void Redirect_can_be_disabled() =>
        Assert.False(CreateResolver(redirectWwwToApex: false)
            .TryGetApexRedirectTarget("www.tripsidekick.net", out _));
}
