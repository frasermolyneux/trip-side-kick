namespace MX.TripSideKick.Web;

/// <summary>
/// Exposes the deployment-verification <c>/info</c> endpoint consumed by
/// <c>frasermolyneux/actions/wait-for-version</c>.
/// </summary>
public static class InfoEndpointExtensions
{
    public static WebApplication MapInfoEndpoint(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/info", () => Results.Ok(new
        {
            Version = BuildInfo.InformationalVersion,
            BuildInfo.BuildVersion,
            BuildInfo.AssemblyVersion
        })).AllowAnonymous();

        return app;
    }
}
