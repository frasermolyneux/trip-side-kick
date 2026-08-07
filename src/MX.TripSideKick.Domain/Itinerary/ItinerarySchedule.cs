using NodaTime;

namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>
/// An <see cref="ItineraryItem"/>'s scheduling state - either "unscheduled" (an idea) or
/// "scheduled" onto a specific day, optionally with a start/end time.
/// </summary>
/// <remarks>
/// Explicit status rather than "the presence of a <c>Date</c> means scheduled" so that
/// "I'm working on the schedule" and "there is no schedule" are distinguishable at the type level -
/// mirrors <see cref="Trips.TripDates"/>'s modelling of trip dates.
/// </remarks>
public readonly record struct ItinerarySchedule
{
    private ItinerarySchedule(ItineraryScheduleStatus status, LocalDate? date, LocalTime? startTime, LocalTime? endTime)
    {
        Status = status;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
    }

    public ItineraryScheduleStatus Status { get; }

    /// <summary>Populated only when <see cref="Status"/> is <see cref="ItineraryScheduleStatus.Scheduled"/>.</summary>
    public LocalDate? Date { get; }

    /// <summary>Optional even when scheduled; a whole-day activity has no start time.</summary>
    public LocalTime? StartTime { get; }

    /// <summary>Optional even when scheduled. When both times are present, must be strictly after <see cref="StartTime"/>.</summary>
    public LocalTime? EndTime { get; }

    public static ItinerarySchedule Unscheduled() =>
        new(ItineraryScheduleStatus.Unscheduled, null, null, null);

    public static ItinerarySchedule Scheduled(LocalDate date, LocalTime? startTime = null, LocalTime? endTime = null)
    {
        ValidateTimeRange(startTime, endTime);
        return new ItinerarySchedule(ItineraryScheduleStatus.Scheduled, date, startTime, endTime);
    }

    private static void ValidateTimeRange(LocalTime? startTime, LocalTime? endTime)
    {
        if (startTime is { } start && endTime is { } end && end <= start)
        {
            throw new ArgumentException(
                "The activity end time must be strictly after its start time.", nameof(endTime));
        }
    }
}
