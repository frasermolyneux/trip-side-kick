using System.Threading.RateLimiting;

using Azure.Identity;

using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

using MX.Observability.ApplicationInsights.AspNetCore;

using MX.TripSideKick.Application;
using MX.TripSideKick.Application.Abstractions;
using MX.TripSideKick.Infrastructure;
using MX.TripSideKick.Infrastructure.Options;
using MX.TripSideKick.Web;
using MX.TripSideKick.Web.Hosting;
using MX.TripSideKick.Web.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Observability ---------------------------------------------------------------------------
// Server telemetry flows to the workload's Application Insights resource, which is attached to the
// shared platform-monitoring Log Analytics workspace. Never emit PII (trip content, documents,
// booking references, email addresses) into telemetry.
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddObservability();

builder.Services.AddOptions<ClientTelemetryOptions>()
    .Bind(builder.Configuration.GetSection(ClientTelemetryOptions.SectionName));

builder.Services.AddOptions<SecurityHeadersOptions>()
    .Bind(builder.Configuration.GetSection(SecurityHeadersOptions.SectionName));

// --- Modular monolith composition ------------------------------------------------------------
builder.Services.AddTripSideKickApplication();
builder.Services.AddTripSideKickInfrastructure(builder.Configuration);

// IDENTITY STUB: no authentication scheme is registered in this slice. The identity slice adds
// Microsoft Entra External ID (B2B collaboration + self-service sign-up) and replaces this
// registration with a claims-backed implementation.
builder.Services.AddSingleton<ICurrentUser, AnonymousCurrentUser>();

// --- Web surface -----------------------------------------------------------------------------
builder.Services.AddHostRouting(builder.Configuration);
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddOpenApi("v1");
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// Same-origin, secure-by-default cookies ready for the identity slice.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-tsk-antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// App Service terminates TLS in front of the app.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

ConfigureDataProtection(builder);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

// Deny-by-default host validation and www -> apex canonicalisation.
app.UseHostRouting();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

var siteHosts = app.Services.SiteHosts();
var appHosts = app.Services.AppHosts();

// Operational endpoints are intentionally reachable on every configured host so that App Service
// health probes and the deployment version gate keep working regardless of surface.
app.MapHealthChecks("/api/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    // Readiness deliberately does not probe SQL or Blob Storage in this slice; nothing is
    // registered with the "ready" tag yet, so the app reports ready once the process is up.
    Predicate = check => check.Tags.Contains("ready")
});
app.MapInfoEndpoint();

// tripsidekick.net -> server-rendered brochure site only.
app.MapRazorPages().RequireHost(siteHosts);

// tripsidekick.app -> versioned API/BFF plus the React PWA shell.
app.MapControllers().RequireHost(appHosts);
app.MapOpenApi("/swagger/{documentName}/openapi.json").RequireHost(appHosts);
app.MapFallbackToFile("index.html").RequireHost(appHosts);

await app.RunAsync().ConfigureAwait(false);

static void ConfigureDataProtection(WebApplicationBuilder webApplicationBuilder)
{
    var dataProtection = webApplicationBuilder.Services.AddDataProtection()
        .SetApplicationName("trip-side-kick");

    var storage = webApplicationBuilder.Configuration
        .GetSection(BlobStorageOptions.SectionName)
        .Get<BlobStorageOptions>();

    if (string.IsNullOrWhiteSpace(storage?.ServiceUri))
    {
        // Local development falls back to the default (file system) key ring.
        return;
    }

    var containerUri = new Uri(
        $"{storage.ServiceUri.TrimEnd('/')}/{storage.DataProtectionContainerName}/keys.xml");

    dataProtection.PersistKeysToAzureBlobStorage(containerUri, new DefaultAzureCredential());
}
