using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

using Moq;

using NodaTime;

namespace MX.TripSideKick.Web.Tests.Application.Itinerary;

public sealed class ItineraryPlanningServiceTests
{
    private readonly Mock<IItineraryRepository> itineraryRepository = new();
    private readonly Mock<IItineraryCommentRepository> commentRepository = new();
    private readonly Mock<ITripActivityFeedRepository> feedRepository = new();
    private readonly Mock<ITripRepository> tripRepository = new();
    private readonly Mock<IMembershipRepository> membershipRepository = new();
    private readonly ItineraryPlanningService sut;

    private readonly TripId tripId = TripId.New();
    private const string SubjectId = "user-1";

    public ItineraryPlanningServiceTests()
    {
        var access = new MembershipAccessService(membershipRepository.Object);
        sut = new ItineraryPlanningService(
            itineraryRepository.Object,
            commentRepository.Object,
            feedRepository.Object,
            tripRepository.Object,
            access,
            new PassthroughUnitOfWork(),
            new FixedClock(Instant.FromUtc(2025, 1, 1, 0, 0)));
    }

    private void GivenMembership(MembershipRole role)
    {
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Membership.Create(tripId, SubjectId, role));
    }

    private void GivenTripWithConfirmedDates(LocalDate start, LocalDate end)
    {
        var trip = Trip.Create("Trip", null, null, TripDates.Confirmed(start, end), null);
        tripRepository.Setup(r => r.GetAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(trip);
    }

    private void GivenTripWithUndecidedDates()
    {
        var trip = Trip.Create("Trip");
        tripRepository.Setup(r => r.GetAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(trip);
    }

    [Fact]
    public async Task Viewer_cannot_create_an_idea()
    {
        GivenMembership(MembershipRole.Viewer);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateIdeaAsync(tripId, SubjectId, new CreateItineraryItemInput("Museum", null, null, null)));
    }

    [Fact]
    public async Task Editor_creating_an_idea_appends_a_feed_entry()
    {
        GivenMembership(MembershipRole.Editor);

        await sut.CreateIdeaAsync(tripId, SubjectId, new CreateItineraryItemInput("Museum", null, null, null));

        itineraryRepository.Verify(r => r.AddAsync(It.IsAny<ItineraryItem>(), It.IsAny<CancellationToken>()), Times.Once);
        feedRepository.Verify(r => r.AddAsync(
            It.Is<TripActivityFeedEntry>(e =>
                e.EventType == TripActivityFeedEventType.ItemCreated
                && e.ActorSubjectId == SubjectId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scheduling_requires_confirmed_dates()
    {
        GivenMembership(MembershipRole.Editor);
        GivenTripWithUndecidedDates();
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItineraryItem.CreateIdea(tripId, "Museum"));

        await Assert.ThrowsAsync<SchedulingNotSupportedException>(() =>
            sut.ScheduleAsync(tripId, ItineraryItemId.New(), SubjectId,
                new ScheduleItineraryItemInput(new LocalDate(2025, 6, 1), null, null),
                new byte[8]));
    }

    [Fact]
    public async Task Scheduling_rejects_a_date_outside_the_trip_window()
    {
        GivenMembership(MembershipRole.Editor);
        GivenTripWithConfirmedDates(new LocalDate(2025, 6, 1), new LocalDate(2025, 6, 5));
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ItineraryItem.CreateIdea(tripId, "Museum"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ScheduleAsync(tripId, ItineraryItemId.New(), SubjectId,
                new ScheduleItineraryItemInput(new LocalDate(2025, 7, 1), null, null),
                new byte[8]));
    }

    [Fact]
    public async Task Scheduling_within_the_confirmed_window_promotes_the_item()
    {
        GivenMembership(MembershipRole.Editor);
        GivenTripWithConfirmedDates(new LocalDate(2025, 6, 1), new LocalDate(2025, 6, 5));
        var item = ItineraryItem.CreateIdea(tripId, "Museum");
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var scheduled = await sut.ScheduleAsync(tripId, ItineraryItemId.New(), SubjectId,
            new ScheduleItineraryItemInput(new LocalDate(2025, 6, 3), null, null),
            new byte[8]);

        Assert.Equal(ItineraryScheduleStatus.Scheduled, scheduled.Schedule.Status);
        feedRepository.Verify(r => r.AddAsync(
            It.Is<TripActivityFeedEntry>(e => e.EventType == TripActivityFeedEventType.ItemScheduled),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Viewer_can_add_a_comment()
    {
        GivenMembership(MembershipRole.Viewer);
        var item = ItineraryItem.CreateIdea(tripId, "Museum");
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var comment = await sut.AddCommentAsync(tripId, item.Id, SubjectId, "Sounds good!");

        Assert.Equal("Sounds good!", comment.Body);
        commentRepository.Verify(r => r.AddAsync(It.IsAny<ItineraryComment>(), It.IsAny<CancellationToken>()), Times.Once);
        feedRepository.Verify(r => r.AddAsync(
            It.Is<TripActivityFeedEntry>(e => e.EventType == TripActivityFeedEventType.CommentAdded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_wipes_comments_and_appends_a_feed_entry()
    {
        GivenMembership(MembershipRole.Editor);
        var item = ItineraryItem.CreateIdea(tripId, "Museum");
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        await sut.DeleteAsync(tripId, item.Id, SubjectId);

        commentRepository.Verify(r => r.RemoveAllForItemAsync(item.Id, It.IsAny<CancellationToken>()), Times.Once);
        itineraryRepository.Verify(r => r.RemoveAsync(item, It.IsAny<CancellationToken>()), Times.Once);
        feedRepository.Verify(r => r.AddAsync(
            It.Is<TripActivityFeedEntry>(e => e.EventType == TripActivityFeedEventType.ItemDeleted),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_propagates_concurrency_conflicts()
    {
        GivenMembership(MembershipRole.Editor);
        var item = ItineraryItem.CreateIdea(tripId, "Museum");
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        itineraryRepository
            .Setup(r => r.UpdateAsync(It.IsAny<ItineraryItem>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("stale"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            sut.UpdateContentAsync(tripId, item.Id, SubjectId,
                new UpdateItineraryItemContentInput("New title", null, null),
                new byte[8]));
    }

    [Fact]
    public async Task Item_lookup_on_wrong_trip_returns_NotFound()
    {
        GivenMembership(MembershipRole.Editor);
        var otherTripId = TripId.New();
        var item = ItineraryItem.CreateIdea(otherTripId, "Museum");
        itineraryRepository
            .Setup(r => r.GetAsync(It.IsAny<ItineraryItemId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateContentAsync(tripId, item.Id, SubjectId,
                new UpdateItineraryItemContentInput("x", null, null), new byte[8]));
    }
}
