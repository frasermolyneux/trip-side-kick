using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Web.Tests.Domain;

public sealed class MembershipPolicyTests
{
    private static readonly TripId AnyTripId = TripId.New();

    [Fact]
    public void EnsureRoleChangeAllowed_blocks_demoting_the_only_owner()
    {
        var owner = Membership.Create(AnyTripId, "owner-subject", MembershipRole.Owner);
        var members = new[] { owner };

        Assert.Throws<LastOwnerViolationException>(() =>
            MembershipPolicy.EnsureRoleChangeAllowed(members, owner, MembershipRole.Editor));
    }

    [Fact]
    public void EnsureRoleChangeAllowed_allows_demoting_an_owner_when_another_owner_exists()
    {
        var owner1 = Membership.Create(AnyTripId, "owner-1", MembershipRole.Owner);
        var owner2 = Membership.Create(AnyTripId, "owner-2", MembershipRole.Owner);
        var members = new[] { owner1, owner2 };

        var exception = Record.Exception(() =>
            MembershipPolicy.EnsureRoleChangeAllowed(members, owner1, MembershipRole.Editor));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureRoleChangeAllowed_allows_promoting_a_non_owner_to_owner()
    {
        var owner = Membership.Create(AnyTripId, "owner-subject", MembershipRole.Owner);
        var editor = Membership.Create(AnyTripId, "editor-subject", MembershipRole.Editor);
        var members = new[] { owner, editor };

        var exception = Record.Exception(() =>
            MembershipPolicy.EnsureRoleChangeAllowed(members, editor, MembershipRole.Owner));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureRoleChangeAllowed_ignores_non_owner_targets()
    {
        var owner = Membership.Create(AnyTripId, "owner-subject", MembershipRole.Owner);
        var viewer = Membership.Create(AnyTripId, "viewer-subject", MembershipRole.Viewer);
        var members = new[] { owner, viewer };

        var exception = Record.Exception(() =>
            MembershipPolicy.EnsureRoleChangeAllowed(members, viewer, MembershipRole.Editor));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureRemovalAllowed_blocks_removing_the_only_owner()
    {
        var owner = Membership.Create(AnyTripId, "owner-subject", MembershipRole.Owner);
        var members = new[] { owner };

        Assert.Throws<LastOwnerViolationException>(() =>
            MembershipPolicy.EnsureRemovalAllowed(members, owner));
    }

    [Fact]
    public void EnsureRemovalAllowed_allows_removing_an_owner_when_another_owner_exists()
    {
        var owner1 = Membership.Create(AnyTripId, "owner-1", MembershipRole.Owner);
        var owner2 = Membership.Create(AnyTripId, "owner-2", MembershipRole.Owner);
        var members = new[] { owner1, owner2 };

        var exception = Record.Exception(() => MembershipPolicy.EnsureRemovalAllowed(members, owner1));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureRemovalAllowed_allows_removing_a_non_owner()
    {
        var owner = Membership.Create(AnyTripId, "owner-subject", MembershipRole.Owner);
        var editor = Membership.Create(AnyTripId, "editor-subject", MembershipRole.Editor);
        var members = new[] { owner, editor };

        var exception = Record.Exception(() => MembershipPolicy.EnsureRemovalAllowed(members, editor));

        Assert.Null(exception);
    }
}
