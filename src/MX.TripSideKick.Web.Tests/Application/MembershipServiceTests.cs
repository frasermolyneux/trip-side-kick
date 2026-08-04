using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

using Moq;

namespace MX.TripSideKick.Web.Tests.Application;

public sealed class MembershipServiceTests
{
    private readonly Mock<IMembershipRepository> membershipRepository = new();
    private readonly MembershipService sut;
    private readonly TripId tripId = TripId.New();

    public MembershipServiceTests()
    {
        var membershipAccess = new MembershipAccessService(membershipRepository.Object);
        sut = new MembershipService(membershipRepository.Object, membershipAccess);
    }

    [Fact]
    public async Task ListMembersAsync_allows_any_member_including_viewers()
    {
        var viewer = Membership.Create(tripId, "viewer-subject", MembershipRole.Viewer);
        var members = new[] { viewer };
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "viewer-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(viewer);
        membershipRepository.Setup(r => r.ListForTripAsync(tripId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await sut.ListMembersAsync(tripId, "viewer-subject");

        Assert.Same(members, result);
    }

    [Fact]
    public async Task ChangeRoleAsync_is_rejected_for_editors_and_viewers()
    {
        var editor = Membership.Create(tripId, "editor-subject", MembershipRole.Editor);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "editor-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.ChangeRoleAsync(tripId, MembershipId.New(), MembershipRole.Editor, "editor-subject", [1]));
    }

    [Fact]
    public async Task ChangeRoleAsync_blocks_demoting_the_last_owner()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        membershipRepository
            .Setup(r => r.ListForTripAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owner]);

        await Assert.ThrowsAsync<LastOwnerViolationException>(() =>
            sut.ChangeRoleAsync(tripId, owner.Id, MembershipRole.Editor, "owner-subject", [1]));
    }

    [Fact]
    public async Task ChangeRoleAsync_allows_demoting_an_owner_when_another_owner_exists()
    {
        var owner1 = Membership.Create(tripId, "owner-1", MembershipRole.Owner);
        var owner2 = Membership.Create(tripId, "owner-2", MembershipRole.Owner);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner1);
        membershipRepository
            .Setup(r => r.ListForTripAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owner1, owner2]);
        membershipRepository
            .Setup(r => r.UpdateAsync(owner1, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await sut.ChangeRoleAsync(tripId, owner1.Id, MembershipRole.Editor, "owner-1", [1]);

        Assert.Equal(MembershipRole.Editor, result.Role);
    }

    [Fact]
    public async Task RemoveMemberAsync_requires_the_owner_role()
    {
        var editor = Membership.Create(tripId, "editor-subject", MembershipRole.Editor);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "editor-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.RemoveMemberAsync(tripId, MembershipId.New(), "editor-subject"));
    }

    [Fact]
    public async Task RemoveMemberAsync_blocks_removing_the_last_owner()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        membershipRepository
            .Setup(r => r.ListForTripAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owner]);

        await Assert.ThrowsAsync<LastOwnerViolationException>(() =>
            sut.RemoveMemberAsync(tripId, owner.Id, "owner-subject"));
    }

    [Fact]
    public async Task LeaveTripAsync_blocks_the_last_owner_from_leaving()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "owner-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        membershipRepository
            .Setup(r => r.ListForTripAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owner]);

        await Assert.ThrowsAsync<LastOwnerViolationException>(() => sut.LeaveTripAsync(tripId, "owner-subject"));
    }

    [Fact]
    public async Task LeaveTripAsync_allows_a_non_owner_to_leave()
    {
        var owner = Membership.Create(tripId, "owner-subject", MembershipRole.Owner);
        var editor = Membership.Create(tripId, "editor-subject", MembershipRole.Editor);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "editor-subject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(editor);
        membershipRepository
            .Setup(r => r.ListForTripAsync(tripId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([owner, editor]);
        membershipRepository.Setup(r => r.RemoveAsync(editor, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await sut.LeaveTripAsync(tripId, "editor-subject");

        membershipRepository.Verify(r => r.RemoveAsync(editor, It.IsAny<CancellationToken>()), Times.Once);
    }
}
