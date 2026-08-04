using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using MX.TripSideKick.Web.Options;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// Supplies the browser bundle with the small amount of runtime configuration it needs.
/// </summary>
/// <remarks>
/// Only non-secret values are ever returned. The Application Insights connection string is an
/// ingestion endpoint identifier and is safe to expose to the browser; it is not a credential.
/// </remarks>
[ApiController]
[Route("v1/client-config")]
[Produces("application/json")]
public sealed class ClientConfigController(IOptions<ClientTelemetryOptions> telemetryOptions) : ControllerBase
{
    private readonly ClientTelemetryOptions telemetryOptions =
        telemetryOptions?.Value ?? throw new ArgumentNullException(nameof(telemetryOptions));

    [HttpGet]
    public ActionResult<ClientConfigResponse> Get() => Ok(new ClientConfigResponse(
        telemetryOptions.ClientConnectionString,
        SignInEnabled: true,
        LoginUrl: "/v1/auth/login",
        LogoutUrl: "/v1/auth/logout"));
}

/// <summary>Response contract for <c>GET /v1/client-config</c>.</summary>
/// <param name="ApplicationInsightsConnectionString">Browser telemetry connection string, if configured.</param>
/// <param name="SignInEnabled">Whether interactive sign-in is available.</param>
/// <param name="LoginUrl">Relative BFF URL the SPA navigates to in order to sign in. Never a client id/authority - the SPA never talks to Entra directly.</param>
/// <param name="LogoutUrl">Relative BFF URL the SPA navigates to in order to sign out.</param>
public sealed record ClientConfigResponse(
    string? ApplicationInsightsConnectionString,
    bool SignInEnabled,
    string LoginUrl,
    string LogoutUrl);
