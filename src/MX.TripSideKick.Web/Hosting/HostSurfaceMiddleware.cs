using Microsoft.AspNetCore.Http.Extensions;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Strict host validation and <c>www</c> canonicalisation for the two public surfaces.
/// </summary>
/// <remarks>
/// Runs before routing so that an unrecognised <c>Host</c> header never reaches application code.
/// The surface itself is enforced per-endpoint with <c>RequireHost</c>; this middleware adds the
/// deny-by-default gate and records the resolved surface for downstream components.
/// </remarks>
public sealed class HostSurfaceMiddleware(RequestDelegate next, HostSurfaceResolver resolver, ILogger<HostSurfaceMiddleware> logger)
{
    /// <summary>Key used to stash the resolved surface in <see cref="HttpContext.Items"/>.</summary>
    public const string HostSurfaceItemKey = "TripSideKick.HostSurface";

    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly HostSurfaceResolver resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly ILogger<HostSurfaceMiddleware> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Request.Host.Host;

        if (resolver.TryGetApexRedirectTarget(host, out var apexHost))
        {
            var port = context.Request.Host.Port;
            var apex = port.HasValue ? new HostString(apexHost, port.Value) : new HostString(apexHost);

            var target = UriHelper.BuildAbsolute(
                context.Request.Scheme,
                apex,
                context.Request.PathBase,
                context.Request.Path,
                context.Request.QueryString);

            context.Response.Redirect(target, permanent: true, preserveMethod: true);
            return;
        }

        var surface = resolver.Resolve(host);

        if (surface == HostSurface.Unknown)
        {
            logger.LogWarning("Rejected request for unrecognised host {RequestHost}.", host);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Unrecognised host.", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        context.Items[HostSurfaceItemKey] = surface;

        await next(context).ConfigureAwait(false);
    }
}
