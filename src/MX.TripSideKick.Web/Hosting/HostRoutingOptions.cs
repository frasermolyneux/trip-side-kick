namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Configures the host-aware split between the brochure site and the application surface.
/// </summary>
/// <remarks>
/// Populated from the <c>HostRouting</c> configuration section. Runtime settings
/// and local launch profiles supply the hostnames for their respective environments.
/// </remarks>
public sealed class HostRoutingOptions
{
    public const string SectionName = "HostRouting";

    /// <summary>Hostnames that serve the Razor Pages brochure site.</summary>
    public IList<string> SiteHosts { get; } = [];

    /// <summary>Hostnames that serve the React PWA and the versioned <c>/v1</c> API.</summary>
    public IList<string> AppHosts { get; } = [];

    /// <summary>When true, <c>www.&lt;apex&gt;</c> is permanently redirected to <c>&lt;apex&gt;</c>.</summary>
    public bool RedirectWwwToApex { get; set; } = true;
}
