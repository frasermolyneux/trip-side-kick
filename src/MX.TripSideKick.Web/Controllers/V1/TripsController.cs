using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;
using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Trips;
using MX.TripSideKick.Web.Api;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// Journey 1 ("start a trip") and the trip-content parts of Journey 2. App hosts only - see
/// <c>Program.cs</c>'s <c>MapControllers().RequireHost(appHosts)</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("v1/trips")]
[Produces("application/json")]
public sealed class TripsController(TripPlanningService tripPlanningService, ICurrentUser currentUser) : ControllerBase
{
    private readonly TripPlanningService tripPlanningService = tripPlanningService
        ?? throw new ArgumentNullException(nameof(tripPlanningService));
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    /// <summary>Creates a trip. The creator becomes its Owner and an account-linked traveller by default.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<TripResponse>> Create(
        [FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trip = await tripPlanningService.CreateTripAsync(
            new CreateTripInput(request.Name, request.Destinations, request.ReportingCurrencyCode, request.Dates?.ToDomain(), request.CoverImageUrl),
            RequireSubjectId(),
            currentUser.DisplayName ?? string.Empty,
            cancellationToken).ConfigureAwait(false);

        var response = TripResponse.From(trip);
        Response.Headers.ETag = RowVersionETag.ToETag(trip.RowVersion);
        return CreatedAtAction(nameof(Get), new { tripId = trip.Id.Value }, response);
    }

    /// <summary>Lists every trip the signed-in user is a member of.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> List(CancellationToken cancellationToken)
    {
        var trips = await tripPlanningService.ListMyTripsAsync(RequireSubjectId(), cancellationToken).ConfigureAwait(false);
        return Ok(trips.Select(TripResponse.From).ToList());
    }

    /// <summary>Gets a single trip. Requires at least the Viewer role.</summary>
    [HttpGet("{tripId:guid}")]
    public async Task<ActionResult<TripResponse>> Get(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await tripPlanningService
            .GetTripAsync(new TripId(tripId), RequireSubjectId(), cancellationToken)
            .ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(trip.RowVersion);
        return Ok(TripResponse.From(trip));
    }

    /// <summary>
    /// Updates trip content. Requires the Editor role or higher and an <c>If-Match</c> header
    /// matching the trip's current ETag - a stale/missing header returns 428/409 rather than
    /// silently overwriting someone else's change.
    /// </summary>
    [HttpPut("{tripId:guid}")]
    public async Task<ActionResult<TripResponse>> Update(
        Guid tripId, [FromBody] UpdateTripRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!RowVersionETag.TryRequireIfMatch(Request, out var expectedRowVersion, out var failure))
        {
            return failure!;
        }

        var trip = await tripPlanningService.UpdateTripAsync(
            new TripId(tripId),
            RequireSubjectId(),
            new UpdateTripInput(request.Name, request.Destinations, request.ReportingCurrencyCode, request.Dates?.ToDomain(), request.CoverImageUrl),
            expectedRowVersion,
            cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(trip.RowVersion);
        return Ok(TripResponse.From(trip));
    }

    private string RequireSubjectId() =>
        currentUser.SubjectId ?? throw new InvalidOperationException("An authenticated request must have a subject id.");
}

/// <summary>Wire representation of <see cref="TripDates"/>.</summary>
/// <param name="Status">One of <c>undecided</c>, <c>approximate</c>, <c>confirmed</c>.</param>
public sealed record TripDatesModel(string Status, DateOnly? StartDate, DateOnly? EndDate)
{
    public static TripDatesModel From(TripDates dates) => new(
        dates.Status.ToString().ToLowerInvariant(),
        ToDateOnly(dates.StartDate),
        ToDateOnly(dates.EndDate));

    public TripDates ToDomain() => Status.ToLowerInvariant() switch
    {
        "confirmed" when StartDate is { } start && EndDate is { } end =>
            TripDates.Confirmed(ToLocalDate(start), ToLocalDate(end)),
        "approximate" => TripDates.Approximate(ToLocalDate(StartDate), ToLocalDate(EndDate)),
        "undecided" => TripDates.Undecided(),
        _ => throw new ArgumentException($"Unrecognised trip date status '{Status}'.", nameof(Status))
    };

    private static DateOnly? ToDateOnly(NodaTime.LocalDate? date) =>
        date is { } value ? new DateOnly(value.Year, value.Month, value.Day) : null;

    private static NodaTime.LocalDate ToLocalDate(DateOnly date) => new(date.Year, date.Month, date.Day);

    private static NodaTime.LocalDate? ToLocalDate(DateOnly? date) =>
        date is { } value ? ToLocalDate(value) : null;
}

/// <summary>Request body for <c>POST /v1/trips</c>.</summary>
public sealed record CreateTripRequest(
    string Name,
    IReadOnlyList<string>? Destinations,
    string? ReportingCurrencyCode,
    TripDatesModel? Dates,
    string? CoverImageUrl);

/// <summary>Request body for <c>PUT /v1/trips/{tripId}</c>. Every field is optional: omitted fields are left unchanged.</summary>
public sealed record UpdateTripRequest(
    string? Name,
    IReadOnlyList<string>? Destinations,
    string? ReportingCurrencyCode,
    TripDatesModel? Dates,
    string? CoverImageUrl);

/// <summary>Response contract for a trip.</summary>
public sealed record TripResponse(
    Guid Id,
    string Name,
    IReadOnlyList<string> Destinations,
    string? ReportingCurrencyCode,
    TripDatesModel Dates,
    string? CoverImageUrl,
    string ETag)
{
    public static TripResponse From(Trip trip) => new(
        trip.Id.Value,
        trip.Name,
        trip.Destinations,
        trip.ReportingCurrencyCode,
        TripDatesModel.From(trip.Dates),
        trip.CoverImageUrl,
        RowVersionETag.ToETag(trip.RowVersion));
}
