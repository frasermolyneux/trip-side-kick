using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// Placeholder v1 endpoint that proves the versioned API surface is wired up and host-scoped to
/// the application domain.
/// </summary>
[ApiController]
[Route("v1/status")]
[Produces("application/json")]
public sealed class StatusController(ICurrentUser currentUser, IHostEnvironment environment) : ControllerBase
{
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    private readonly IHostEnvironment environment = environment ?? throw new ArgumentNullException(nameof(environment));

    [HttpGet]
    public ActionResult<StatusResponse> Get() => Ok(new StatusResponse(
        environment.EnvironmentName,
        currentUser.IsAuthenticated,
        // TODO (identity slice): remove once Entra External ID sign-in is wired up.
        AuthenticationStubbed: true));
}

/// <summary>Response contract for <c>GET /v1/status</c>.</summary>
/// <param name="Environment">The ASP.NET Core environment name.</param>
/// <param name="Authenticated">Whether the caller is authenticated (always false in this slice).</param>
/// <param name="AuthenticationStubbed">Whether sign-in is stubbed out rather than implemented.</param>
public sealed record StatusResponse(string Environment, bool Authenticated, bool AuthenticationStubbed);
