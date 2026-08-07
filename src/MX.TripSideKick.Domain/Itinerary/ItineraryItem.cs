using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>
/// Itinerary item aggregate root: a parked idea (<see cref="ItineraryScheduleStatus.Unscheduled"/>)
/// or a scheduled activity (<see cref="ItineraryScheduleStatus.Scheduled"/>). One aggregate, one
/// table - promotion from idea to activity is a state transition on the same entity, not a copy
/// into a second aggregate, so nothing is lost or duplicated when the plan firms up.
/// </summary>
/// <remarks>
/// Confirmed-trip-dates gating (an activity can only be scheduled when the trip's dates are
/// <see cref="TripDateStatus.Confirmed"/>) is enforced by the application layer, since this
/// aggregate deliberately does not know about <c>Trip</c>. Traveller applicability follows the
/// "empty list means everyone" encoding described on
/// <see cref="TravellerApplicability"/> so the field can stay a plain <c>List&lt;TravellerId&gt;</c>
/// without a separate "everyone" flag.
/// </remarks>
public sealed class ItineraryItem
{
    private readonly List<TravellerId> applicableTravellerIds = [];

    private ItineraryItem()
    {
    }

    public ItineraryItemId Id { get; private init; }

    public TripId TripId { get; private init; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Optional freeform notes. Trip content, so may contain PII - never log.</summary>
    public string? Notes { get; private set; }

    /// <summary>Optional freeform location label. Not a Maps integration in this slice - just a string.</summary>
    public string? Location { get; private set; }

    public ItinerarySchedule Schedule { get; private set; } = ItinerarySchedule.Unscheduled();

    /// <summary>
    /// The travellers this item applies to. An empty list means "applies to everyone on the trip"
    /// (see <see cref="TravellerApplicability"/>).
    /// </summary>
    public IReadOnlyList<TravellerId> ApplicableTravellerIds => applicableTravellerIds;

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    /// <summary>Maximum allowed length of <see cref="Title"/>.</summary>
    public const int MaxTitleLength = 200;

    public static ItineraryItem CreateIdea(
        TripId tripId,
        string title,
        string? notes = null,
        string? location = null,
        IEnumerable<TravellerId>? applicableTravellerIds = null)
    {
        var item = new ItineraryItem
        {
            Id = ItineraryItemId.New(),
            TripId = tripId,
            Schedule = ItinerarySchedule.Unscheduled()
        };

        item.UpdateContent(title, notes, location);
        if (applicableTravellerIds is not null)
        {
            item.SetApplicability(applicableTravellerIds);
        }

        return item;
    }

    public void UpdateContent(string title, string? notes, string? location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Itinerary item title cannot be longer than {MaxTitleLength} characters.", nameof(title));
        }

        Title = title.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
    }

    /// <summary>
    /// Places the item on a specific day, promoting an idea to a scheduled activity. Named
    /// <c>PlaceOnDay</c> rather than <c>Schedule</c> so the property below can remain named
    /// <see cref="Schedule"/> - C# does not allow a property and method to share a name.
    /// </summary>
    public void PlaceOnDay(ItinerarySchedule schedule)
    {
        if (schedule.Status != ItineraryScheduleStatus.Scheduled)
        {
            throw new ArgumentException(
                "Use Unschedule() to move an item back to an idea; Schedule requires a Scheduled state.",
                nameof(schedule));
        }

        Schedule = schedule;
    }

    /// <summary>Demotes a scheduled activity back to an idea by clearing the schedule.</summary>
    public void Unschedule() => Schedule = ItinerarySchedule.Unscheduled();

    public void SetApplicability(IEnumerable<TravellerId> travellerIds)
    {
        ArgumentNullException.ThrowIfNull(travellerIds);
        applicableTravellerIds.Clear();
        applicableTravellerIds.AddRange(travellerIds.Distinct());
    }
}
