using MX.TripSideKick.Domain.Travellers;

using NodaTime;

namespace MX.TripSideKick.Application.Itinerary;

/// <summary>Input for creating a new idea via <see cref="ItineraryPlanningService.CreateIdeaAsync"/>.</summary>
public sealed record CreateItineraryItemInput(
    string Title,
    string? Notes = null,
    string? Location = null,
    IReadOnlyList<TravellerId>? ApplicableTravellerIds = null);

/// <summary>Input for editing an existing item's content.</summary>
public sealed record UpdateItineraryItemContentInput(string Title, string? Notes, string? Location);

/// <summary>Input for placing an idea onto a day (also used to reschedule an existing activity).</summary>
public sealed record ScheduleItineraryItemInput(LocalDate Date, LocalTime? StartTime, LocalTime? EndTime);

/// <summary>Input for setting the applicable-traveller list. An empty list means "everyone".</summary>
public sealed record SetItineraryItemApplicabilityInput(IReadOnlyList<TravellerId> TravellerIds);
