namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Static assets belonging to the Razor Pages brochure surface.
/// </summary>
/// <remarks>
/// These live outside <c>wwwroot</c> on purpose. Vite owns <c>wwwroot</c> and empties it on every
/// client build, and the brochure surface must not be able to serve the PWA bundle, so the two
/// asset roots are kept physically separate and mounted on their own host surfaces.
/// </remarks>
public static class SiteAssets
{
    /// <summary>Content-root-relative directory holding the brochure surface's static assets.</summary>
    public const string DirectoryName = "SiteAssets";
}
