using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Application.Itinerary;

/// <summary>
/// Application service for Journey 5 ("itinerary and collaborative planning"): ideas, day-by-day
/// scheduling, per-traveller applicability, comments, and the collaborative activity feed. Every
/// mutation appends a <see cref="TripActivityFeedEntry"/> in the same unit of work so the feed can
/// never diverge from the underlying data.
/// </summary>
public sealed class ItineraryPlanningService(
    IItineraryRepository itineraryRepository,
    IItineraryCommentRepository commentRepository,
    ITripActivityFeedRepository activityFeedRepository,
    ITripRepository tripRepository,
    MembershipAccessService membershipAccess,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int MaxActivityFeedEntries = 100;

    private readonly IItineraryRepository itineraryRepository = itineraryRepository ?? throw new ArgumentNullException(nameof(itineraryRepository));
    private readonly IItineraryCommentRepository commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
    private readonly ITripActivityFeedRepository activityFeedRepository = activityFeedRepository ?? throw new ArgumentNullException(nameof(activityFeedRepository));
    private readonly ITripRepository tripRepository = tripRepository ?? throw new ArgumentNullException(nameof(tripRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess ?? throw new ArgumentNullException(nameof(membershipAccess));
    private readonly IUnitOfWork unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly IClock clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<IReadOnlyList<ItineraryItem>> ListItemsAsync(
        TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken).ConfigureAwait(false);
        return await itineraryRepository.ListForTripAsync(tripId, cancellationToken).ConfigureAwait(false);
    }

    public Task<ItineraryItem> CreateIdeaAsync(
        TripId tripId, string subjectId, CreateItineraryItemInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, ct).ConfigureAwait(false);

            var item = ItineraryItem.CreateIdea(tripId, input.Title, input.Notes, input.Location, input.ApplicableTravellerIds);
            await itineraryRepository.AddAsync(item, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.ItemCreated,
                $"Idea added: {item.Title}", item.Id, ct).ConfigureAwait(false);

            return item;
        }, cancellationToken);
    }

    public Task<ItineraryItem> UpdateContentAsync(
        TripId tripId,
        ItineraryItemId itemId,
        string subjectId,
        UpdateItineraryItemContentInput input,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, ct).ConfigureAwait(false);

            var item = await LoadOnTripAsync(tripId, itemId, ct).ConfigureAwait(false);
            item.UpdateContent(input.Title, input.Notes, input.Location);
            await itineraryRepository.UpdateAsync(item, expectedRowVersion, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.ItemUpdated,
                $"Updated: {item.Title}", item.Id, ct).ConfigureAwait(false);

            return item;
        }, cancellationToken);
    }

    public Task<ItineraryItem> ScheduleAsync(
        TripId tripId,
        ItineraryItemId itemId,
        string subjectId,
        ScheduleItineraryItemInput input,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, ct).ConfigureAwait(false);

            var trip = await tripRepository.GetAsync(tripId, ct).ConfigureAwait(false)
                ?? throw new NotFoundException("Trip not found.");

            if (!trip.Dates.SupportsDayByDayScheduling)
            {
                throw new SchedulingNotSupportedException(
                    "Trip dates must be confirmed before activities can be scheduled.");
            }

            if (trip.Dates.StartDate is { } start && trip.Dates.EndDate is { } end)
            {
                if (input.Date < start || input.Date > end)
                {
                    throw new ArgumentException(
                        "The scheduled date must fall between the trip's start and end dates.", nameof(input));
                }
            }

            var item = await LoadOnTripAsync(tripId, itemId, ct).ConfigureAwait(false);
            item.PlaceOnDay(ItinerarySchedule.Scheduled(input.Date, input.StartTime, input.EndTime));
            await itineraryRepository.UpdateAsync(item, expectedRowVersion, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.ItemScheduled,
                $"Scheduled: {item.Title}", item.Id, ct).ConfigureAwait(false);

            return item;
        }, cancellationToken);
    }

    public Task<ItineraryItem> UnscheduleAsync(
        TripId tripId,
        ItineraryItemId itemId,
        string subjectId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, ct).ConfigureAwait(false);

            var item = await LoadOnTripAsync(tripId, itemId, ct).ConfigureAwait(false);
            item.Unschedule();
            await itineraryRepository.UpdateAsync(item, expectedRowVersion, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.ItemUnscheduled,
                $"Moved back to ideas: {item.Title}", item.Id, ct).ConfigureAwait(false);

            return item;
        }, cancellationToken);
    }

    public Task<ItineraryItem> SetApplicabilityAsync(
        TripId tripId,
        ItineraryItemId itemId,
        string subjectId,
        SetItineraryItemApplicabilityInput input,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, ct).ConfigureAwait(false);

            var item = await LoadOnTripAsync(tripId, itemId, ct).ConfigureAwait(false);
            item.SetApplicability(input.TravellerIds);
            await itineraryRepository.UpdateAsync(item, expectedRowVersion, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.ItemUpdated,
                $"Applicability changed: {item.Title}", item.Id, ct).ConfigureAwait(false);

            return item;
        }, cancellationToken);
    }

    public Task DeleteAsync(
        TripId tripId, ItineraryItemId itemId, string subjectId, CancellationToken cancellationToken = default) =>
        unitOfWork.ExecuteAsync(async ct =>
        {
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, ct).ConfigureAwait(false);

            var item = await LoadOnTripAsync(tripId, itemId, ct).ConfigureAwait(false);
            var title = item.Title;

            // Comments are append-only children with no aggregate root of their own; wipe them out
            // alongside the parent so a re-created item id can never resurrect stale threads.
            await commentRepository.RemoveAllForItemAsync(itemId, ct).ConfigureAwait(false);
            await itineraryRepository.RemoveAsync(item, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.ItemDeleted,
                $"Deleted: {title}", itineraryItemId: null, ct).ConfigureAwait(false);
        }, cancellationToken);

    public async Task<IReadOnlyList<ItineraryComment>> ListCommentsAsync(
        TripId tripId, ItineraryItemId itemId, string subjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken).ConfigureAwait(false);
        // Verify the parent item lives on this trip before returning its comment thread.
        _ = await LoadOnTripAsync(tripId, itemId, cancellationToken).ConfigureAwait(false);

        return await commentRepository.ListForItemAsync(itemId, cancellationToken).ConfigureAwait(false);
    }

    public Task<ItineraryComment> AddCommentAsync(
        TripId tripId, ItineraryItemId itemId, string subjectId, string body, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExecuteAsync(async ct =>
        {
            // Viewer+ deliberately: commenting is the one mutation a Viewer is allowed to perform
            // on trip content in this slice.
            await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, ct).ConfigureAwait(false);

            var item = await LoadOnTripAsync(tripId, itemId, ct).ConfigureAwait(false);
            var now = clock.GetCurrentInstant();
            var comment = ItineraryComment.Create(tripId, item.Id, subjectId, body, now);
            await commentRepository.AddAsync(comment, ct).ConfigureAwait(false);

            await AppendFeedAsync(tripId, subjectId, TripActivityFeedEventType.CommentAdded,
                $"Commented on: {item.Title}", item.Id, ct).ConfigureAwait(false);

            return comment;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<TripActivityFeedEntry>> ListActivityFeedAsync(
        TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken).ConfigureAwait(false);
        return await activityFeedRepository.ListForTripAsync(tripId, MaxActivityFeedEntries, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ItineraryItem> LoadOnTripAsync(
        TripId tripId, ItineraryItemId itemId, CancellationToken cancellationToken)
    {
        var item = await itineraryRepository.GetAsync(itemId, cancellationToken).ConfigureAwait(false);
        // Treat "item exists on a different trip" as "item does not exist" so callers cannot probe
        // other trips' item ids via 403 vs 404 differences (see ForbiddenException remarks).
        if (item is null || item.TripId != tripId)
        {
            throw new NotFoundException("Itinerary item not found.");
        }

        return item;
    }

    private Task AppendFeedAsync(
        TripId tripId,
        string actorSubjectId,
        TripActivityFeedEventType eventType,
        string summary,
        ItineraryItemId? itineraryItemId,
        CancellationToken cancellationToken)
    {
        var entry = TripActivityFeedEntry.Create(
            tripId, actorSubjectId, eventType, summary, clock.GetCurrentInstant(), itineraryItemId);
        return activityFeedRepository.AddAsync(entry, cancellationToken);
    }
}
