using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MX.TripSideKick.Application.Abstractions;
using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;
using MX.TripSideKick.Web.Api;

using NodaTime;

namespace MX.TripSideKick.Web.Controllers.V1;

/// <summary>
/// Journey 5 ("itinerary and collaborative planning") — ideas, activities, applicability, comments,
/// activity feed, and per-member persistent traveller filters. App hosts only via
/// <c>Program.cs</c>'s <c>MapControllers().RequireHost(appHosts)</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("v1/trips/{tripId:guid}/itinerary")]
[Produces("application/json")]
public sealed class ItineraryController(
    ItineraryPlanningService itineraryPlanning,
    TripTravellerFilterService travellerFilterService,
    ICurrentUser currentUser) : ControllerBase
{
    private readonly ItineraryPlanningService itineraryPlanning = itineraryPlanning
        ?? throw new ArgumentNullException(nameof(itineraryPlanning));
    private readonly TripTravellerFilterService travellerFilterService = travellerFilterService
        ?? throw new ArgumentNullException(nameof(travellerFilterService));
    private readonly ICurrentUser currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

    // ---------- Items ----------

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<ItineraryItemResponse>>> ListItems(
        Guid tripId, CancellationToken cancellationToken)
    {
        var subjectId = RequireSubjectId();
        var items = await itineraryPlanning.ListItemsAsync(new TripId(tripId), subjectId, cancellationToken).ConfigureAwait(false);
        var filter = await travellerFilterService.GetOrCreateForCallerAsync(new TripId(tripId), subjectId, cancellationToken).ConfigureAwait(false);
        var effective = await travellerFilterService.ResolveEffectiveTravellerIdsAsync(filter, cancellationToken).ConfigureAwait(false);

        var visible = items
            .Where(item => TripTravellerFilterEvaluator.IsVisible(filter.Mode, effective, item.ApplicableTravellerIds))
            .Select(ItineraryItemResponse.From)
            .ToList();
        return Ok(visible);
    }

    [HttpPost("items")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ItineraryItemResponse>> CreateIdea(
        Guid tripId, [FromBody] CreateItineraryItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = await itineraryPlanning.CreateIdeaAsync(
            new TripId(tripId),
            RequireSubjectId(),
            new CreateItineraryItemInput(
                request.Title,
                request.Notes,
                request.Location,
                (request.ApplicableTravellerIds ?? []).Select(id => new TravellerId(id)).ToList()),
            cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(item.RowVersion);
        return CreatedAtAction(nameof(ListItems), new { tripId }, ItineraryItemResponse.From(item));
    }

    [HttpPut("items/{itemId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ItineraryItemResponse>> UpdateContent(
        Guid tripId, Guid itemId, [FromBody] UpdateItineraryItemContentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!RowVersionETag.TryRequireIfMatch(Request, out var expected, out var failure))
        {
            return failure!;
        }

        var item = await itineraryPlanning.UpdateContentAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(),
            new UpdateItineraryItemContentInput(request.Title, request.Notes, request.Location),
            expected, cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(item.RowVersion);
        return Ok(ItineraryItemResponse.From(item));
    }

    [HttpPut("items/{itemId:guid}/schedule")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ItineraryItemResponse>> Schedule(
        Guid tripId, Guid itemId, [FromBody] ScheduleItineraryItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!RowVersionETag.TryRequireIfMatch(Request, out var expected, out var failure))
        {
            return failure!;
        }

        var input = new ScheduleItineraryItemInput(
            ToLocalDate(request.Date),
            ToLocalTime(request.StartTime),
            ToLocalTime(request.EndTime));

        var item = await itineraryPlanning.ScheduleAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(),
            input, expected, cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(item.RowVersion);
        return Ok(ItineraryItemResponse.From(item));
    }

    [HttpDelete("items/{itemId:guid}/schedule")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ItineraryItemResponse>> Unschedule(
        Guid tripId, Guid itemId, CancellationToken cancellationToken)
    {
        if (!RowVersionETag.TryRequireIfMatch(Request, out var expected, out var failure))
        {
            return failure!;
        }

        var item = await itineraryPlanning.UnscheduleAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(),
            expected, cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(item.RowVersion);
        return Ok(ItineraryItemResponse.From(item));
    }

    [HttpPut("items/{itemId:guid}/applicability")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ItineraryItemResponse>> SetApplicability(
        Guid tripId, Guid itemId, [FromBody] SetApplicabilityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!RowVersionETag.TryRequireIfMatch(Request, out var expected, out var failure))
        {
            return failure!;
        }

        var item = await itineraryPlanning.SetApplicabilityAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(),
            new SetItineraryItemApplicabilityInput(
                (request.TravellerIds ?? []).Select(id => new TravellerId(id)).ToList()),
            expected, cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(item.RowVersion);
        return Ok(ItineraryItemResponse.From(item));
    }

    [HttpDelete("items/{itemId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid tripId, Guid itemId, CancellationToken cancellationToken)
    {
        await itineraryPlanning.DeleteAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    // ---------- Comments ----------

    [HttpGet("items/{itemId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<ItineraryCommentResponse>>> ListComments(
        Guid tripId, Guid itemId, CancellationToken cancellationToken)
    {
        var comments = await itineraryPlanning.ListCommentsAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);
        return Ok(comments.Select(ItineraryCommentResponse.From).ToList());
    }

    [HttpPost("items/{itemId:guid}/comments")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ItineraryCommentResponse>> AddComment(
        Guid tripId, Guid itemId, [FromBody] AddCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var comment = await itineraryPlanning.AddCommentAsync(
            new TripId(tripId), new ItineraryItemId(itemId), RequireSubjectId(), request.Body, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(ListComments), new { tripId, itemId }, ItineraryCommentResponse.From(comment));
    }

    // ---------- Activity feed ----------

    [HttpGet("activity-feed")]
    public async Task<ActionResult<IReadOnlyList<TripActivityFeedEntryResponse>>> ListActivityFeed(
        Guid tripId, CancellationToken cancellationToken)
    {
        var entries = await itineraryPlanning.ListActivityFeedAsync(
            new TripId(tripId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);
        return Ok(entries.Select(TripActivityFeedEntryResponse.From).ToList());
    }

    // ---------- Traveller filter ----------

    [HttpGet("traveller-filter")]
    public async Task<ActionResult<TripTravellerFilterResponse>> GetTravellerFilter(
        Guid tripId, CancellationToken cancellationToken)
    {
        var filter = await travellerFilterService.GetOrCreateForCallerAsync(
            new TripId(tripId), RequireSubjectId(), cancellationToken).ConfigureAwait(false);
        Response.Headers.ETag = RowVersionETag.ToETag(filter.RowVersion);
        return Ok(TripTravellerFilterResponse.From(filter));
    }

    [HttpPut("traveller-filter")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<TripTravellerFilterResponse>> SetTravellerFilter(
        Guid tripId, [FromBody] SetTravellerFilterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!RowVersionETag.TryRequireIfMatch(Request, out var expected, out var failure))
        {
            return failure!;
        }

        var mode = ParseMode(request.Mode);
        var selected = (request.SelectedTravellerIds ?? []).Select(id => new TravellerId(id)).ToList();

        var filter = await travellerFilterService.UpdateForCallerAsync(
            new TripId(tripId), RequireSubjectId(), mode, selected, expected, cancellationToken).ConfigureAwait(false);

        Response.Headers.ETag = RowVersionETag.ToETag(filter.RowVersion);
        return Ok(TripTravellerFilterResponse.From(filter));
    }

    private string RequireSubjectId() =>
        currentUser.SubjectId ?? throw new InvalidOperationException("An authenticated request must have a subject id.");

    private static TravellerFilterMode ParseMode(string? mode) => (mode ?? "everyone").ToLowerInvariant() switch
    {
        "everyone" => TravellerFilterMode.Everyone,
        "me" => TravellerFilterMode.Me,
        "selected" => TravellerFilterMode.Selected,
        _ => throw new ArgumentException($"Unrecognised traveller filter mode '{mode}'.", nameof(mode))
    };

    private static LocalDate ToLocalDate(DateOnly date) => new(date.Year, date.Month, date.Day);

    private static LocalTime? ToLocalTime(TimeOnly? time) =>
        time is { } value ? new LocalTime(value.Hour, value.Minute, value.Second, value.Millisecond) : null;
}

// ---------- DTOs (inline records) ----------

public sealed record CreateItineraryItemRequest(
    string Title,
    string? Notes,
    string? Location,
    IReadOnlyList<Guid>? ApplicableTravellerIds);

public sealed record UpdateItineraryItemContentRequest(string Title, string? Notes, string? Location);

public sealed record ScheduleItineraryItemRequest(DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime);

public sealed record SetApplicabilityRequest(IReadOnlyList<Guid>? TravellerIds);

public sealed record AddCommentRequest(string Body);

public sealed record SetTravellerFilterRequest(string Mode, IReadOnlyList<Guid>? SelectedTravellerIds);

public sealed record ItineraryScheduleResponse(string Status, DateOnly? Date, TimeOnly? StartTime, TimeOnly? EndTime)
{
    public static ItineraryScheduleResponse From(ItinerarySchedule schedule) => new(
        schedule.Status.ToString().ToLowerInvariant(),
        schedule.Date is { } d ? new DateOnly(d.Year, d.Month, d.Day) : null,
        schedule.StartTime is { } start ? new TimeOnly(start.Hour, start.Minute, start.Second, start.Millisecond) : null,
        schedule.EndTime is { } end ? new TimeOnly(end.Hour, end.Minute, end.Second, end.Millisecond) : null);
}

public sealed record ItineraryItemResponse(
    Guid Id,
    Guid TripId,
    string Title,
    string? Notes,
    string? Location,
    ItineraryScheduleResponse Schedule,
    IReadOnlyList<Guid> ApplicableTravellerIds,
    string ETag)
{
    public static ItineraryItemResponse From(ItineraryItem item) => new(
        item.Id.Value,
        item.TripId.Value,
        item.Title,
        item.Notes,
        item.Location,
        ItineraryScheduleResponse.From(item.Schedule),
        item.ApplicableTravellerIds.Select(id => id.Value).ToList(),
        RowVersionETag.ToETag(item.RowVersion));
}

public sealed record ItineraryCommentResponse(
    Guid Id,
    Guid TripId,
    Guid ItineraryItemId,
    string AuthorSubjectId,
    string Body,
    DateTimeOffset CreatedAt)
{
    public static ItineraryCommentResponse From(ItineraryComment comment) => new(
        comment.Id.Value,
        comment.TripId.Value,
        comment.ItineraryItemId.Value,
        comment.AuthorSubjectId,
        comment.Body,
        comment.CreatedAt.ToDateTimeOffset());
}

public sealed record TripActivityFeedEntryResponse(
    Guid Id,
    Guid TripId,
    string ActorSubjectId,
    string EventType,
    string Summary,
    DateTimeOffset OccurredAt,
    Guid? ItineraryItemId)
{
    public static TripActivityFeedEntryResponse From(TripActivityFeedEntry entry) => new(
        entry.Id.Value,
        entry.TripId.Value,
        entry.ActorSubjectId,
        entry.EventType.ToString(),
        entry.Summary,
        entry.OccurredAt.ToDateTimeOffset(),
        entry.ItineraryItemId?.Value);
}

public sealed record TripTravellerFilterResponse(
    Guid Id,
    Guid TripId,
    Guid MembershipId,
    string Mode,
    IReadOnlyList<Guid> SelectedTravellerIds,
    string ETag)
{
    public static TripTravellerFilterResponse From(TripTravellerFilter filter) => new(
        filter.Id.Value,
        filter.TripId.Value,
        filter.MembershipId.Value,
        filter.Mode.ToString().ToLowerInvariant(),
        filter.SelectedTravellerIds.Select(id => id.Value).ToList(),
        RowVersionETag.ToETag(filter.RowVersion));
}
