using Microsoft.Extensions.Options;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Registration and pipeline helpers for the host-aware <c>.net</c> / <c>.app</c> split.
/// </summary>
public static class HostRoutingExtensions
{
    public static IServiceCollection AddHostRouting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HostRoutingOptions>()
            .Bind(configuration.GetSection(HostRoutingOptions.SectionName))
            .Validate(
                options => options.SiteHosts.Count > 0 && options.AppHosts.Count > 0,
                $"'{HostRoutingOptions.SectionName}:SiteHosts' and '{HostRoutingOptions.SectionName}:AppHosts' must both be configured.")
            .ValidateOnStart();

        services.AddSingleton<HostSurfaceResolver>();

        return services;
    }

    public static IApplicationBuilder UseHostRouting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<HostSurfaceMiddleware>();
    }

    /// <summary>
    /// The surface resolved by <see cref="HostSurfaceMiddleware"/> for the current request, or
    /// <see cref="HostSurface.Unknown"/> when that middleware has not run yet.
    /// </summary>
    public static HostSurface GetHostSurface(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(HostSurfaceMiddleware.HostSurfaceItemKey, out var value)
            && value is HostSurface surface
            ? surface
            : HostSurface.Unknown;
    }

    /// <summary>Hostnames allowed to serve the brochure site.</summary>
    public static string[] SiteHosts(this IServiceProvider services) =>
        [.. services.GetRequiredService<IOptions<HostRoutingOptions>>().Value.SiteHosts];

    /// <summary>Hostnames allowed to serve the PWA shell and the versioned API.</summary>
    public static string[] AppHosts(this IServiceProvider services) =>
        [.. services.GetRequiredService<IOptions<HostRoutingOptions>>().Value.AppHosts];
}
