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
        // TODO (identity slice): replace with the Entra External ID authority/client id once sign-in exists.
        SignInEnabled: false));
}

/// <summary>Response contract for <c>GET /v1/client-config</c>.</summary>
/// <param name="ApplicationInsightsConnectionString">Browser telemetry connection string, if configured.</param>
/// <param name="SignInEnabled">Whether interactive sign-in is available (false while identity is stubbed).</param>
public sealed record ClientConfigResponse(string? ApplicationInsightsConnectionString, bool SignInEnabled);
