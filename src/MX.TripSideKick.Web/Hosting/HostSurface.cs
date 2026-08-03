namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// The public surface a request was addressed to.
/// </summary>
/// <remarks>
/// A single App Service deployment serves two distinct products:
/// <list type="bullet">
///   <item><description><see cref="Site"/> — the <c>tripsidekick.net</c> Razor Pages brochure site.</description></item>
///   <item><description><see cref="App"/> — the <c>tripsidekick.app</c> React PWA and versioned <c>/v1</c> API/BFF.</description></item>
/// </list>
/// </remarks>
public enum HostSurface
{
    /// <summary>The host is not configured for either surface; the request is rejected.</summary>
    Unknown = 0,

    /// <summary>Public brochure / marketing site (<c>tripsidekick.net</c>).</summary>
    Site = 1,

    /// <summary>Application surface (<c>tripsidekick.app</c>): PWA shell plus versioned API.</summary>
    App = 2
}
