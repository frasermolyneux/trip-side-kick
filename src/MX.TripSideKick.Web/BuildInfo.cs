using System.Reflection;

namespace MX.TripSideKick.Web;

/// <summary>
/// Build/version metadata stamped by Nerdbank.GitVersioning, surfaced to operators through the
/// <c>/info</c> endpoint and the brochure site footer.
/// </summary>
public static class BuildInfo
{
    private const string Unknown = "unknown";

    /// <summary>Full informational version, e.g. <c>0.1.5-preview+g1a2b3c4d5e</c>.</summary>
    public static string InformationalVersion { get; } =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Unknown;

    /// <summary>Version without the git height/commit suffix, e.g. <c>0.1.5-preview</c>.</summary>
    public static string BuildVersion { get; } = InformationalVersion.Split('+')[0];

    /// <summary>Assembly version, e.g. <c>0.1.0.0</c>.</summary>
    public static string AssemblyVersion { get; } =
        typeof(BuildInfo).Assembly.GetName().Version?.ToString() ?? Unknown;
}
