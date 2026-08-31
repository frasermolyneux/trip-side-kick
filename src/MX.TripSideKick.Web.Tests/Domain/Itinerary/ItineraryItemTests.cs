using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Web.Tests.Domain.Itinerary;

public sealed class ItineraryItemTests
{
    private static readonly TripId AnyTripId = TripId.New();

    [Fact]
    public void CreateIdea_returns_an_unscheduled_item_that_applies_to_everyone_by_default()
    {
        var item = ItineraryItem.CreateIdea(AnyTripId, "Visit the harbour");

        Assert.Equal(AnyTripId, item.TripId);
        Assert.Equal("Visit the harbour", item.Title);
        Assert.Equal(ItineraryScheduleStatus.Unscheduled, item.Schedule.Status);
        Assert.Empty(item.ApplicableTravellerIds);
        Assert.True(TravellerApplicability.AppliesToEveryone(item.ApplicableTravellerIds));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateIdea_rejects_a_blank_title(string title) =>
        Assert.ThrowsAny<ArgumentException>(() => ItineraryItem.CreateIdea(AnyTripId, title));

    [Fact]
    public void CreateIdea_rejects_a_title_over_the_max_length()
    {
        var tooLong = new string('a', ItineraryItem.MaxTitleLength + 1);
        Assert.Throws<ArgumentException>(() => ItineraryItem.CreateIdea(AnyTripId, tooLong));
    }

    [Fact]
    public void PlaceOnDay_promotes_an_idea_into_a_scheduled_activity()
    {
        var item = ItineraryItem.CreateIdea(AnyTripId, "Museum");
        item.PlaceOnDay(ItinerarySchedule.Scheduled(new LocalDate(2025, 6, 1), new LocalTime(10, 0), new LocalTime(12, 0)));

        Assert.Equal(ItineraryScheduleStatus.Scheduled, item.Schedule.Status);
        Assert.Equal(new LocalDate(2025, 6, 1), item.Schedule.Date);
    }

    [Fact]
    public void PlaceOnDay_rejects_an_unscheduled_schedule_value()
    {
        var item = ItineraryItem.CreateIdea(AnyTripId, "Museum");
        Assert.Throws<ArgumentException>(() => item.PlaceOnDay(ItinerarySchedule.Unscheduled()));
    }

    [Fact]
    public void Unschedule_demotes_an_activity_back_to_an_idea()
    {
        var item = ItineraryItem.CreateIdea(AnyTripId, "Museum");
        item.PlaceOnDay(ItinerarySchedule.Scheduled(new LocalDate(2025, 6, 1)));
        item.Unschedule();

        Assert.Equal(ItineraryScheduleStatus.Unscheduled, item.Schedule.Status);
        Assert.Null(item.Schedule.Date);
    }

    [Fact]
    public void SetApplicability_deduplicates_traveller_ids()
    {
        var item = ItineraryItem.CreateIdea(AnyTripId, "Museum");
        var t1 = TravellerId.New();
        item.SetApplicability([t1, t1]);
        Assert.Single(item.ApplicableTravellerIds);
    }
}

public sealed class ItineraryScheduleTests
{
    [Fact]
    public void Scheduled_rejects_end_before_start()
    {
        Assert.Throws<ArgumentException>(() =>
            ItinerarySchedule.Scheduled(new LocalDate(2025, 6, 1), new LocalTime(12, 0), new LocalTime(10, 0)));
    }

    [Fact]
    public void Scheduled_rejects_end_equal_to_start()
    {
        Assert.Throws<ArgumentException>(() =>
            ItinerarySchedule.Scheduled(new LocalDate(2025, 6, 1), new LocalTime(10, 0), new LocalTime(10, 0)));
    }

    [Fact]
    public void Scheduled_accepts_null_times() =>
        Assert.Equal(ItineraryScheduleStatus.Scheduled,
            ItinerarySchedule.Scheduled(new LocalDate(2025, 6, 1)).Status);

    [Fact]
    public void Unscheduled_has_null_date_and_times()
    {
        var s = ItinerarySchedule.Unscheduled();
        Assert.Null(s.Date);
        Assert.Null(s.StartTime);
        Assert.Null(s.EndTime);
    }
}
