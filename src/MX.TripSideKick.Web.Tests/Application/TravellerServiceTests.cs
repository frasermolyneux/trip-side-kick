using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using Moq;

namespace MX.TripSideKick.Web.Tests.Application;

public sealed class TravellerServiceTests
{
    private readonly Mock<ITravellerRepository> travellerRepository = new();
    private readonly Mock<IMembershipRepository> membershipRepository = new();
    private readonly TravellerService sut;
    private readonly TripId tripId = TripId.New();

    public TravellerServiceTests()
    {
        var membershipAccess = new MembershipAccessService(membershipRepository.Object);
        sut = new TravellerService(travellerRepository.Object, membershipAccess);
    }

    [Fact]
    public async Task LinkSelfAsTravellerAsync_throws_when_already_linked()
    {
        var membership = Membership.Create(tripId, "subject-1", MembershipRole.Owner);
        var existingTraveller = Traveller.Create(tripId, "Existing", membership.Id);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        travellerRepository
            .Setup(r => r.GetLinkedToMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTraveller);

        await Assert.ThrowsAsync<AlreadyMemberException>(() =>
            sut.LinkSelfAsTravellerAsync(tripId, "subject-1", "New name"));
    }

    [Fact]
    public async Task LinkSelfAsTravellerAsync_creates_a_traveller_linked_to_the_callers_membership()
    {
        var membership = Membership.Create(tripId, "subject-1", MembershipRole.Editor);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        travellerRepository
            .Setup(r => r.GetLinkedToMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Traveller?)null);
        travellerRepository
            .Setup(r => r.AddAsync(It.IsAny<Traveller>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var traveller = await sut.LinkSelfAsTravellerAsync(tripId, "subject-1", "Sam");

        Assert.Equal(membership.Id, traveller.LinkedMembershipId);
        Assert.Equal("Sam", traveller.DisplayName);
    }

    [Fact]
    public async Task UnlinkSelfAsTravellerAsync_throws_when_not_linked()
    {
        var membership = Membership.Create(tripId, "subject-1", MembershipRole.Owner);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        travellerRepository
            .Setup(r => r.GetLinkedToMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Traveller?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.UnlinkSelfAsTravellerAsync(tripId, "subject-1"));
    }

    [Fact]
    public async Task UnlinkSelfAsTravellerAsync_removes_the_traveller_but_the_caller_keeps_their_membership_role()
    {
        // The point of this test: an Owner can remove themselves as a traveller without losing
        // ownership - only the Traveller record is removed, never the Membership.
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        var traveller = Traveller.Create(tripId, "Owner", owner.Id);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        travellerRepository
            .Setup(r => r.GetLinkedToMembershipAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traveller);
        travellerRepository.Setup(r => r.RemoveAsync(traveller, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.UnlinkSelfAsTravellerAsync(tripId, "owner-subject");

        travellerRepository.Verify(r => r.RemoveAsync(traveller, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(MembershipRole.Owner, owner.Role);
    }
}
