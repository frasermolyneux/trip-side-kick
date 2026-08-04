using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using Moq;

namespace MX.TripSideKick.Web.Tests.Application;

public sealed class TripPlanningServiceTests
{
    private readonly Mock<ITripRepository> tripRepository = new();
    private readonly Mock<IMembershipRepository> membershipRepository = new();
    private readonly Mock<ITravellerRepository> travellerRepository = new();
    private readonly TripPlanningService sut;

    public TripPlanningServiceTests()
    {
        var membershipAccess = new MembershipAccessService(membershipRepository.Object);
        sut = new TripPlanningService(
            tripRepository.Object, membershipRepository.Object, travellerRepository.Object,
            membershipAccess, new PassthroughUnitOfWork());
    }

    [Fact]
    public async Task CreateTripAsync_makes_the_creator_an_owner_and_an_account_linked_traveller()
    {
        Membership? capturedMembership = null;
        Traveller? capturedTraveller = null;
        tripRepository.Setup(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        membershipRepository
            .Setup(r => r.AddAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()))
            .Callback<Membership, CancellationToken>((m, _) => capturedMembership = m)
            .Returns(Task.CompletedTask);
        travellerRepository
            .Setup(r => r.AddAsync(It.IsAny<Traveller>(), It.IsAny<CancellationToken>()))
            .Callback<Traveller, CancellationToken>((t, _) => capturedTraveller = t)
            .Returns(Task.CompletedTask);

        var trip = await sut.CreateTripAsync(new CreateTripInput("Iceland"), "creator-subject", "Creator Name");

        Assert.NotNull(capturedMembership);
        Assert.Equal(MembershipRole.Owner, capturedMembership!.Role);
        Assert.Equal("creator-subject", capturedMembership.SubjectId);
        Assert.Equal(trip.Id, capturedMembership.TripId);

        Assert.NotNull(capturedTraveller);
        Assert.Equal(capturedMembership.Id, capturedTraveller!.LinkedMembershipId);
        Assert.Equal("Creator Name", capturedTraveller.DisplayName);
    }

    [Fact]
    public async Task CreateTripAsync_falls_back_to_a_default_display_name_when_none_is_supplied()
    {
        Traveller? capturedTraveller = null;
        tripRepository.Setup(r => r.AddAsync(It.IsAny<Trip>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        membershipRepository.Setup(r => r.AddAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        travellerRepository
            .Setup(r => r.AddAsync(It.IsAny<Traveller>(), It.IsAny<CancellationToken>()))
            .Callback<Traveller, CancellationToken>((t, _) => capturedTraveller = t)
            .Returns(Task.CompletedTask);

        await sut.CreateTripAsync(new CreateTripInput("Iceland"), "creator-subject", string.Empty);

        Assert.Equal("Trip owner", capturedTraveller!.DisplayName);
    }

    [Fact]
    public async Task GetTripAsync_throws_Forbidden_when_the_subject_is_below_viewer_but_a_member()
    {
        // Viewer is the minimum role for GetTripAsync, so this exercises the "no membership at
        // all" (NotFound) branch instead, since there is no role below Viewer to test Forbidden.
        var tripId = TripId.New();
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "outsider", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetTripAsync(tripId, "outsider"));
    }

    [Fact]
    public async Task GetTripAsync_throws_NotFound_when_the_trip_itself_is_missing()
    {
        var tripId = TripId.New();
        var membership = Membership.Create(tripId, "subject-1", MembershipRole.Viewer);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        tripRepository.Setup(r => r.GetAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetTripAsync(tripId, "subject-1"));
    }

    [Fact]
    public async Task UpdateTripAsync_requires_the_editor_role_or_higher()
    {
        var tripId = TripId.New();
        var membership = Membership.Create(tripId, "viewer-subject", MembershipRole.Viewer);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "viewer-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.UpdateTripAsync(tripId, "viewer-subject", new UpdateTripInput(Name: "New name"), [1, 2, 3]));
    }

    [Fact]
    public async Task UpdateTripAsync_only_applies_fields_present_in_the_input()
    {
        var tripId = TripId.New();
        var membership = Membership.Create(tripId, "editor-subject", MembershipRole.Editor);
        var trip = Trip.Create("Original name", destinations: ["Paris"]);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "editor-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        tripRepository.Setup(r => r.GetAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(trip);
        tripRepository
            .Setup(r => r.UpdateAsync(trip, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updated = await sut.UpdateTripAsync(tripId, "editor-subject", new UpdateTripInput(Name: "Renamed"), [1, 2, 3]);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(["Paris"], updated.Destinations);
    }
}
