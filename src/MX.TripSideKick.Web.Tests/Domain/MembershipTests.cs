using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Web.Tests.Domain;

public sealed class MembershipTests
{
    [Fact]
    public void Create_requires_a_non_empty_subject_id()
    {
        Assert.Throws<ArgumentException>(() => Membership.Create(TripId.New(), string.Empty, MembershipRole.Owner));
        Assert.Throws<ArgumentException>(() => Membership.Create(TripId.New(), "   ", MembershipRole.Owner));
    }

    [Fact]
    public void Create_sets_the_requested_role()
    {
        var membership = Membership.Create(TripId.New(), "subject-1", MembershipRole.Editor);

        Assert.Equal(MembershipRole.Editor, membership.Role);
    }

    [Fact]
    public void ChangeRole_updates_the_role()
    {
        var membership = Membership.Create(TripId.New(), "subject-1", MembershipRole.Viewer);

        membership.ChangeRole(MembershipRole.Owner);

        Assert.Equal(MembershipRole.Owner, membership.Role);
    }

    [Theory]
    [InlineData(MembershipRole.Viewer, MembershipRole.Editor, false)]
    [InlineData(MembershipRole.Editor, MembershipRole.Owner, false)]
    [InlineData(MembershipRole.Owner, MembershipRole.Editor, true)]
    [InlineData(MembershipRole.Editor, MembershipRole.Viewer, true)]
    public void Role_ordering_supports_minimum_role_comparisons(MembershipRole role, MembershipRole minimum, bool expectedAtLeast)
    {
        Assert.Equal(expectedAtLeast, role >= minimum);
    }
}
