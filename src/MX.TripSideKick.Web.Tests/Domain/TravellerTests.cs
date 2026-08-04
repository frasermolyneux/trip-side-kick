using MX.TripSideKick.Domain.Trips;
using MX.TripSideKick.Domain.Travellers;

namespace MX.TripSideKick.Web.Tests.Domain;

public sealed class TravellerTests
{
    [Fact]
    public void Create_requires_a_non_empty_display_name()
    {
        Assert.Throws<ArgumentException>(() => Traveller.Create(TripId.New(), string.Empty));
        Assert.Throws<ArgumentException>(() => Traveller.Create(TripId.New(), "   "));
    }

    [Fact]
    public void Create_without_a_linked_membership_represents_a_non_travelling_or_unlinked_traveller()
    {
        var traveller = Traveller.Create(TripId.New(), "Alex");

        Assert.Null(traveller.LinkedMembershipId);
    }

    [Fact]
    public void Rename_requires_a_non_empty_display_name()
    {
        var traveller = Traveller.Create(TripId.New(), "Alex");

        Assert.Throws<ArgumentException>(() => traveller.Rename(string.Empty));
        Assert.Equal("Alex", traveller.DisplayName);
    }

    [Fact]
    public void LinkToMembership_then_UnlinkFromMembership_round_trips()
    {
        var traveller = Traveller.Create(TripId.New(), "Alex");
        var membershipId = MX.TripSideKick.Domain.Memberships.MembershipId.New();

        traveller.LinkToMembership(membershipId);
        Assert.Equal(membershipId, traveller.LinkedMembershipId);

        traveller.UnlinkFromMembership();
        Assert.Null(traveller.LinkedMembershipId);
    }
}
