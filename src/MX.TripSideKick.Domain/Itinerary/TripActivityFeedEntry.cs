using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>
/// Append-only entry in a trip's collaborative activity feed ("who changed what, when?"), scoped to
/// itinerary events in this slice but generic enough that future slices (bookings, costs, ...) can
/// append their own events with new <see cref="TripActivityFeedEventType"/> values without a new
/// table.
/// </summary>
/// <remarks>
/// <see cref="Summary"/> is trip content the app service builds (e.g. "Idea added: Reykjavik walking
/// tour") - trip content is allowed here, but PII like email/display name must never be baked into
/// it. Actor identity is stored as the stable subject id only; the API layer resolves that to a
/// linked traveller's display name (or a generic label) before returning it to the client.
/// </remarks>
public sealed class TripActivityFeedEntry
{
    private TripActivityFeedEntry()
    {
    }

    public TripActivityFeedEntryId Id { get; private init; }

    public TripId TripId { get; private init; }

    public string ActorSubjectId { get; private init; } = string.Empty;

    public TripActivityFeedEventType EventType { get; private init; }

    public string Summary { get; private init; } = string.Empty;

    public Instant OccurredAt { get; private init; }

    public ItineraryItemId? ItineraryItemId { get; private init; }

    public static TripActivityFeedEntry Create(
        TripId tripId,
        string actorSubjectId,
        TripActivityFeedEventType eventType,
        string summary,
        Instant occurredAt,
        ItineraryItemId? itineraryItemId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorSubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        return new TripActivityFeedEntry
        {
            Id = TripActivityFeedEntryId.New(),
            TripId = tripId,
            ActorSubjectId = actorSubjectId,
            EventType = eventType,
            Summary = summary,
            OccurredAt = occurredAt,
            ItineraryItemId = itineraryItemId
        };
    }
}
