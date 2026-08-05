using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;
using MX.TripSideKick.Web.Api;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// The minimal traveller list - who a trip is being planned <em>for</em>. Full per-activity
/// assignment/filtering is deferred to a later "Journey 10" slice. App hosts only.
/// </summary>
[ApiController]
[Authorize]
[Route("v1/trips/{tripId:guid}/travellers")]
[Produces("application/json")]
public sealed class TravellersController(TravellerService travellerService, ICurrentUser currentUser) : ControllerBase
{
    private readonly TravellerService travellerService = travellerService
        ?? throw new ArgumentNullException(nameof(travellerService));
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    /// <summary>Lists travellers on the trip. Requires at least the Viewer role.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TravellerResponse>>> List(Guid tripId, CancellationToken cancellationToken)
    {
        var travellers = await travellerService
            .ListTravellersAsync(new TripId(tripId), RequireSubjectId(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(travellers.Select(TravellerResponse.From).ToList());
    }

    /// <summary>Links the signed-in member as a traveller on this trip.</summary>
    [HttpPost("self")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<TravellerResponse>> LinkSelf(
        Guid tripId, [FromBody] LinkSelfAsTravellerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? currentUser.DisplayName ?? "Traveller"
            : request.DisplayName;

        var traveller = await travellerService
            .LinkSelfAsTravellerAsync(new TripId(tripId), RequireSubjectId(), displayName, cancellationToken)
            .ConfigureAwait(false);

        return Ok(TravellerResponse.From(traveller));
    }

    /// <summary>
    /// Removes the signed-in member as a traveller without affecting their membership/role - an
    /// Owner can do this and keeps ownership of the trip.
    /// </summary>
    [HttpDelete("self")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlinkSelf(Guid tripId, CancellationToken cancellationToken)
    {
        await travellerService
            .UnlinkSelfAsTravellerAsync(new TripId(tripId), RequireSubjectId(), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    private string RequireSubjectId() =>
        currentUser.SubjectId ?? throw new InvalidOperationException("An authenticated request must have a subject id.");
}

/// <summary>Request body for <c>POST /v1/trips/{tripId}/travellers/self</c>.</summary>
public sealed record LinkSelfAsTravellerRequest(string? DisplayName);

/// <summary>Response contract for a traveller.</summary>
public sealed record TravellerResponse(Guid Id, Guid TripId, string DisplayName, Guid? LinkedMembershipId)
{
    public static TravellerResponse From(Traveller traveller) => new(
        traveller.Id.Value,
        traveller.TripId.Value,
        traveller.DisplayName,
        traveller.LinkedMembershipId?.Value);
}
