using Microsoft.Extensions.Options;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Maps an inbound hostname to the <see cref="HostSurface"/> it is allowed to serve.
/// </summary>
public sealed class HostSurfaceResolver
{
    private readonly IOptionsMonitor<HostRoutingOptions> options;

    public HostSurfaceResolver(IOptionsMonitor<HostRoutingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    public HostSurface Resolve(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return HostSurface.Unknown;
        }

        var current = options.CurrentValue;

        if (Contains(current.AppHosts, host))
        {
            return HostSurface.App;
        }

        return Contains(current.SiteHosts, host) ? HostSurface.Site : HostSurface.Unknown;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="host"/> is a <c>www.</c> alias of a configured apex
    /// host that should be redirected.
    /// </summary>
    public bool TryGetApexRedirectTarget(string? host, out string apexHost)
    {
        apexHost = string.Empty;

        var current = options.CurrentValue;

        if (!current.RedirectWwwToApex || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (!host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = host[4..];

        if (!Contains(current.AppHosts, candidate) && !Contains(current.SiteHosts, candidate))
        {
            return false;
        }

        apexHost = candidate;
        return true;
    }

    private static bool Contains(IEnumerable<string> hosts, string host) =>
        hosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));
}
