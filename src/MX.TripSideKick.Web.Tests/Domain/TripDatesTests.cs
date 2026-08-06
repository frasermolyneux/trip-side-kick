using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Web.Tests.Domain;

public sealed class TripDatesTests
{
    [Fact]
    public void Undecided_has_no_dates_and_does_not_support_day_by_day_scheduling()
    {
        var dates = TripDates.Undecided();

        Assert.Equal(TripDateStatus.Undecided, dates.Status);
        Assert.Null(dates.StartDate);
        Assert.Null(dates.EndDate);
        Assert.False(dates.SupportsDayByDayScheduling);
    }

    [Fact]
    public void Approximate_allows_partial_or_missing_dates_and_does_not_support_day_by_day_scheduling()
    {
        var dates = TripDates.Approximate(startDate: LocalDate.FromDateTime(new DateTime(2026, 6, 1)));

        Assert.Equal(TripDateStatus.Approximate, dates.Status);
        Assert.NotNull(dates.StartDate);
        Assert.Null(dates.EndDate);
        Assert.False(dates.SupportsDayByDayScheduling);
    }

    [Fact]
    public void Confirmed_requires_both_dates_and_supports_day_by_day_scheduling()
    {
        var start = LocalDate.FromDateTime(new DateTime(2026, 6, 1));
        var end = LocalDate.FromDateTime(new DateTime(2026, 6, 10));

        var dates = TripDates.Confirmed(start, end);

        Assert.Equal(TripDateStatus.Confirmed, dates.Status);
        Assert.Equal(start, dates.StartDate);
        Assert.Equal(end, dates.EndDate);
        Assert.True(dates.SupportsDayByDayScheduling);
    }

    [Fact]
    public void Confirmed_rejects_an_end_date_before_the_start_date()
    {
        var start = LocalDate.FromDateTime(new DateTime(2026, 6, 10));
        var end = LocalDate.FromDateTime(new DateTime(2026, 6, 1));

        Assert.Throws<ArgumentException>(() => TripDates.Confirmed(start, end));
    }

    [Fact]
    public void Approximate_rejects_an_end_date_before_the_start_date_when_both_are_supplied()
    {
        var start = LocalDate.FromDateTime(new DateTime(2026, 6, 10));
        var end = LocalDate.FromDateTime(new DateTime(2026, 6, 1));

        Assert.Throws<ArgumentException>(() => TripDates.Approximate(start, end));
    }
}
