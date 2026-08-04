using System.Threading.RateLimiting;

using Azure.Identity;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;

using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;

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

// Entra External ID (B2B collaboration + self-service sign-up) sign-in for the app surface only.
// Microsoft.Identity.Web authenticates the confidential client with a signed assertion from the
// App Service's system-assigned managed identity (AzureAd:ClientCredentials:0:SourceType =
// SignedAssertionFromManagedIdentity) - no client secret, no certificate. Tokens are redeemed and
// held server-side; the browser only ever receives the session cookie below.
builder.Services
    .AddAuthentication(options =>
    {
        // Explicit rather than relying on Microsoft.Identity.Web's own scheme defaults: the
        // session cookie must be the DefaultScheme so ordinary authenticated requests are
        // resolved from the cookie, while only an explicit challenge (the /v1/auth/login
        // endpoint) starts the OpenID Connect handshake.
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    // Forces the authorization-code + PKCE response type (ResponseType=code) instead of
    // Microsoft.Identity.Web's sign-in-only default (implicit id_token). Without this, no code is
    // ever redeemed at the token endpoint, so the managed-identity federated credential configured
    // via AzureAd:ClientCredentials (see terraform/identity.tf) would never actually be exercised.
    // The in-memory cache holds the resulting tokens server-side only; nothing reaches the cookie
    // (SaveTokens = false below) or the browser.
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    // __Host- requires Secure, Path=/, and no explicit Domain - all true of the ASP.NET Core
    // cookie-auth defaults this app runs behind (see docs/identity-and-access.md).
    options.Cookie.Name = "__Host-tsk-auth";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/v1/auth/login";
});

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    // Access/refresh tokens never need to leave the redemption call; nothing downstream calls
    // Microsoft Graph or another API on the user's behalf in this slice, so nothing needs to be
    // persisted into the auth cookie.
    options.SaveTokens = false;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// --- Web surface -----------------------------------------------------------------------------
builder.Services.AddHostRouting(builder.Configuration);
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddOpenApi("v1");
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// Same-origin, secure-by-default cookies ready for the identity slice.
//
// MinimumSameSitePolicy is deliberately NOT set here (left Unspecified): the OpenID Connect
// handler's nonce/correlation cookies must keep SameSite=None, because the identity provider's
// callback to /signin-oidc is a cross-site POST. If CookiePolicyMiddleware forced every cookie up
// to SameSite=Lax, the browser would never return the None-scoped nonce/correlation cookies on
// that cross-site POST, and sign-in would fail with a correlation error on every attempt. Every
// cookie this app itself issues (the auth cookie above, antiforgery below) already sets its own
// explicit SameSite=Lax, so nothing relies on this policy to be same-origin safe.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
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

// Must run first: App Service terminates TLS, so Request.Scheme is only correct after the forwarded
// headers are applied. HSTS and HTTPS redirection both short-circuit on a non-HTTPS scheme.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Deny-by-default host validation and www -> apex canonicalisation. Runs before HTTPS redirection so
// that a redirect Location is never built from an unrecognised Host header.
app.UseHostRouting();

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

// Static assets are host-scoped as well as endpoints: the generated PWA bundle (index.html, the
// service worker, the web manifest and the hashed assets) belongs to the application surface only,
// and the brochure site serves its own asset root. Without this split UseStaticFiles would happily
// hand /index.html to tripsidekick.net and defeat the MapFallbackToFile host restriction below.
app.UseWhen(
    static context => context.GetHostSurface() == HostSurface.App,
    branch => branch.UseStaticFiles());

app.UseWhen(
    static context => context.GetHostSurface() == HostSurface.Site,
    branch => branch.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(app.Environment.ContentRootPath, SiteAssets.DirectoryName))
    }));

app.UseCookiePolicy();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
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
