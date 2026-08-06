using NodaTime;

namespace MX.TripSideKick.Domain.Trips;

/// <summary>
/// A trip's dates, modelled with an explicit <see cref="TripDateStatus"/> rather than bare
/// nullable dates, so "the traveller skipped this" is distinguishable from "confirmed but I
/// haven't loaded it yet" and callers can tell whether day-by-day scheduling is available.
/// </summary>
public readonly record struct TripDates
{
    private TripDates(TripDateStatus status, LocalDate? startDate, LocalDate? endDate)
    {
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
    }

    public TripDateStatus Status { get; }

    /// <summary>Populated only when <see cref="Status"/> is <see cref="TripDateStatus.Approximate"/> or <see cref="TripDateStatus.Confirmed"/>.</summary>
    public LocalDate? StartDate { get; }

    /// <summary>Populated only when <see cref="Status"/> is <see cref="TripDateStatus.Approximate"/> or <see cref="TripDateStatus.Confirmed"/>.</summary>
    public LocalDate? EndDate { get; }

    /// <summary>Confirmed, exact dates unlock date-dependent capabilities (day-by-day scheduling, offline).</summary>
    public bool SupportsDayByDayScheduling => Status == TripDateStatus.Confirmed;

    public static TripDates Undecided() => new(TripDateStatus.Undecided, null, null);

    public static TripDates Approximate(LocalDate? startDate = null, LocalDate? endDate = null)
    {
        ValidateRange(startDate, endDate);
        return new TripDates(TripDateStatus.Approximate, startDate, endDate);
    }

    public static TripDates Confirmed(LocalDate startDate, LocalDate endDate)
    {
        ValidateRange(startDate, endDate);
        return new TripDates(TripDateStatus.Confirmed, startDate, endDate);
    }

    private static void ValidateRange(LocalDate? startDate, LocalDate? endDate)
    {
        if (startDate is { } start && endDate is { } end && end < start)
        {
            throw new ArgumentException("Trip end date cannot be before the start date.", nameof(endDate));
        }
    }
}
