using Microsoft.Extensions.Options;

namespace MX.TripSideKick.Web.Hosting;

/// <summary>
/// Baseline browser security headers, including a Content Security Policy that is already
/// compatible with the Google Maps JavaScript API origins the app will adopt.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
{
    private readonly RequestDelegate next = next ?? throw new ArgumentNullException(nameof(next));

    private readonly string contentSecurityPolicy =
        (options?.Value ?? throw new ArgumentNullException(nameof(options))).BuildContentSecurityPolicy();

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;

        headers.ContentSecurityPolicy = contentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), payment=(), geolocation=(self)";

        return next(context);
    }
}
