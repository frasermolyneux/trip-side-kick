namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Computes the environment-aware absolute URL of the app surface, used by brochure pages that
/// link users across from the <c>site</c> surface to the <c>app</c> surface.
/// </summary>
public static class AppSurfaceLinkResolver
{
    private const string ProductionAppUrl = "https://tripsidekick.app/";
    private const string DevelopmentAppUrl = "https://dev.tripsidekick.app/";
    private const string LocalAppUrl = "https://localhost:7207/";

    /// <summary>Resolves the app surface URL that corresponds to the given brochure request host.</summary>
    public static string Resolve(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return ProductionAppUrl;
        }

        if (host.StartsWith("dev.", StringComparison.OrdinalIgnoreCase))
        {
            return DevelopmentAppUrl;
        }

        if (host.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return LocalAppUrl;
        }

        return ProductionAppUrl;
    }
}
