using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

using Moq;

namespace MX.TripSideKick.Web.Tests.Application;

public sealed class MembershipAccessServiceTests
{
    private readonly Mock<IMembershipRepository> membershipRepository = new(MockBehavior.Strict);
    private readonly MembershipAccessService sut;

    public MembershipAccessServiceTests()
    {
        sut = new MembershipAccessService(membershipRepository.Object);
    }

    [Fact]
    public async Task RequireRoleAsync_throws_NotFound_when_the_subject_has_no_membership()
    {
        var tripId = TripId.New();
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.RequireRoleAsync(tripId, "subject-1", MembershipRole.Viewer));
    }

    [Fact]
    public async Task RequireRoleAsync_throws_Forbidden_when_the_role_is_below_the_minimum()
    {
        var tripId = TripId.New();
        var membership = Membership.Create(tripId, "subject-1", MembershipRole.Viewer);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.RequireRoleAsync(tripId, "subject-1", MembershipRole.Editor));
    }

    [Theory]
    [InlineData(MembershipRole.Editor, MembershipRole.Viewer)]
    [InlineData(MembershipRole.Owner, MembershipRole.Editor)]
    [InlineData(MembershipRole.Owner, MembershipRole.Owner)]
    public async Task RequireRoleAsync_returns_the_membership_when_the_role_meets_the_minimum(
        MembershipRole actualRole, MembershipRole minimumRole)
    {
        var tripId = TripId.New();
        var membership = Membership.Create(tripId, "subject-1", actualRole);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, "subject-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await sut.RequireRoleAsync(tripId, "subject-1", minimumRole);

        Assert.Same(membership, result);
    }
}
