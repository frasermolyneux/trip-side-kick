namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Content Security Policy inputs. Defaults are same-origin plus the origins required by
/// Application Insights browser telemetry and the Google Maps JavaScript API.
/// </summary>
public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    public IList<string> ScriptSources { get; } = [];

    public IList<string> StyleSources { get; } = [];

    public IList<string> ImageSources { get; } = [];

    public IList<string> ConnectSources { get; } = [];

    public IList<string> FontSources { get; } = [];

    internal string BuildContentSecurityPolicy()
    {
        var directives = new[]
        {
            "default-src 'self'",
            Directive("script-src", "'self'", ScriptSources, "https://maps.googleapis.com"),
            Directive("style-src", "'self'", StyleSources, "https://fonts.googleapis.com"),
            Directive("img-src", "'self' data: blob:", ImageSources, "https://maps.gstatic.com", "https://maps.googleapis.com"),
            Directive("font-src", "'self'", FontSources, "https://fonts.gstatic.com"),
            Directive("connect-src", "'self'", ConnectSources, "https://maps.googleapis.com", "https://*.in.applicationinsights.azure.com", "https://*.livediagnostics.monitor.azure.com"),
            "worker-src 'self'",
            "manifest-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'"
        };

        return string.Join("; ", directives);
    }

    private static string Directive(string name, string baseSources, IEnumerable<string> configured, params string[] defaults)
    {
        var extra = configured.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var sources = extra.Length > 0 ? extra : defaults;

        return sources.Length > 0
            ? $"{name} {baseSources} {string.Join(' ', sources)}"
            : $"{name} {baseSources}";
    }
}
