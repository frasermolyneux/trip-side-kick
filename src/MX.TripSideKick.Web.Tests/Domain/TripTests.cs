using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Web.Tests.Domain;

public sealed class TripTests
{
    [Fact]
    public void Create_requires_a_non_empty_name()
    {
        Assert.Throws<ArgumentException>(() => Trip.Create(string.Empty));
        Assert.Throws<ArgumentException>(() => Trip.Create("   "));
    }

    [Fact]
    public void Create_leaves_optional_fields_unset_when_skipped()
    {
        var trip = Trip.Create("Interrail");

        Assert.Empty(trip.Destinations);
        Assert.Null(trip.ReportingCurrencyCode);
        Assert.Null(trip.CoverImageUrl);
        Assert.Equal(TripDateStatus.Undecided, trip.Dates.Status);
    }

    [Fact]
    public void Create_normalizes_a_valid_currency_code_to_upper_invariant()
    {
        var trip = Trip.Create("Japan", reportingCurrencyCode: "jpy");

        Assert.Equal("JPY", trip.ReportingCurrencyCode);
    }

    [Fact]
    public void Create_rejects_an_unrecognised_currency_code()
    {
        Assert.Throws<ArgumentException>(() => Trip.Create("Japan", reportingCurrencyCode: "ZZZ"));
    }

    [Fact]
    public void Rename_requires_a_non_empty_name()
    {
        var trip = Trip.Create("Original");

        Assert.Throws<ArgumentException>(() => trip.Rename(string.Empty));
        Assert.Equal("Original", trip.Name);
    }

    [Fact]
    public void SetDestinations_trims_whitespace_and_drops_blank_entries()
    {
        var trip = Trip.Create("Road trip");

        trip.SetDestinations(["  Paris  ", string.Empty, "  ", "Lyon"]);

        Assert.Equal(["Paris", "Lyon"], trip.Destinations);
    }

    [Fact]
    public void SetDestinations_replaces_the_previous_list_rather_than_appending()
    {
        var trip = Trip.Create("Road trip", destinations: ["Paris"]);

        trip.SetDestinations(["Lyon"]);

        Assert.Equal(["Lyon"], trip.Destinations);
    }

    [Fact]
    public void SetReportingCurrency_rejects_an_unrecognised_code_and_leaves_the_existing_value_intact()
    {
        var trip = Trip.Create("Japan", reportingCurrencyCode: "JPY");

        Assert.Throws<ArgumentException>(() => trip.SetReportingCurrency("ZZZ"));
        Assert.Equal("JPY", trip.ReportingCurrencyCode);
    }

    [Fact]
    public void SetReportingCurrency_accepts_null_to_clear_the_currency()
    {
        var trip = Trip.Create("Japan", reportingCurrencyCode: "JPY");

        trip.SetReportingCurrency(null);

        Assert.Null(trip.ReportingCurrencyCode);
    }

    [Fact]
    public void SetCoverImage_treats_whitespace_as_no_cover()
    {
        var trip = Trip.Create("Cover test", coverImageUrl: "https://example.test/cover.jpg");

        trip.SetCoverImage("   ");

        Assert.Null(trip.CoverImageUrl);
    }

    [Fact]
    public void SetDates_replaces_the_dates_value()
    {
        var trip = Trip.Create("Dated trip");
        var confirmed = TripDates.Confirmed(
            NodaTime.LocalDate.FromDateTime(new DateTime(2026, 6, 1)),
            NodaTime.LocalDate.FromDateTime(new DateTime(2026, 6, 10)));

        trip.SetDates(confirmed);

        Assert.Equal(TripDateStatus.Confirmed, trip.Dates.Status);
        Assert.True(trip.Dates.SupportsDayByDayScheduling);
    }
}
