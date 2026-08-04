namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Content Security Policy inputs. Built-in defaults cover same-origin plus the origins required by
/// Application Insights browser telemetry and the Google Maps JavaScript API; configured values are
/// added to those defaults rather than replacing them.
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
            // 'unsafe-inline' is required here (not just for style-src-elem/<style> tags): MUI's
            // Popper-based components (Select, Menu, Autocomplete, Tooltip, Dialog, Snackbar
            // transitions) position and animate themselves by writing directly to `element.style`
            // from JS. CSP nonces only ever cover <style>/<link> element emission (e.g. an Emotion
            // cache nonce), never inline `style` *attribute* mutations - so a nonce-based approach
            // would not let us remove 'unsafe-inline' for this specific case, only for injected
            // <style> tags. Without this directive every MUI popover/dropdown silently fails to
            // render - discovered via Playwright E2E when the invite-role MUI <Select> dropdown
            // never opened.
            //
            // Accepted, deliberate risk: this widens the CSP to allow attacker-controlled inline
            // styles (a UI-redress/clickjacking-style vector even with script-src locked down),
            // in exchange for the app's UI framework actually rendering. script-src has no
            // 'unsafe-inline' and stays fully locked down, which is what stops script injection -
            // the primary XSS vector. See docs/testing.md for the fuller writeup.
            Directive("style-src", "'self' 'unsafe-inline'", StyleSources, "https://fonts.googleapis.com"),
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

    /// <summary>
    /// Configured sources are additive: they extend the built-in defaults rather than replacing
    /// them, so adding one origin cannot silently drop telemetry or mapping origins.
    /// </summary>
    private static string Directive(string name, string baseSources, IEnumerable<string> configured, params string[] defaults)
    {
        var sources = defaults
            .Concat(configured.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return sources.Length > 0
            ? $"{name} {baseSources} {string.Join(' ', sources)}"
            : $"{name} {baseSources}";
    }
}
