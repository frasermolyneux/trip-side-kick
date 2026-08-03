using MX.TripSideKick.Web.Hosting;

namespace MX.TripSideKick.Web.Tests.Hosting;

public sealed class AppSurfaceLinkResolverTests
{
    [Theory]
    [InlineData(null, "https://tripsidekick.app/")]
    [InlineData("", "https://tripsidekick.app/")]
    [InlineData("tripsidekick.net", "https://tripsidekick.app/")]
    [InlineData("www.tripsidekick.net", "https://tripsidekick.app/")]
    [InlineData("dev.tripsidekick.net", "https://dev.tripsidekick.app/")]
    [InlineData("DEV.TRIPSIDEKICK.NET", "https://dev.tripsidekick.app/")]
    [InlineData("site.localhost", "https://localhost:7207/")]
    [InlineData("127.0.0.1", "https://tripsidekick.app/")]
    public void Resolve_maps_brochure_host_to_app_surface_url(string? host, string expected) =>
        Assert.Equal(expected, AppSurfaceLinkResolver.Resolve(host));
}
