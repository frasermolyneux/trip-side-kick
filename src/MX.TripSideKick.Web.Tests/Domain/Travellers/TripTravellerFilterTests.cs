using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Web.Tests.Domain.Travellers;

public sealed class TravellerApplicabilityTests
{
    [Fact]
    public void Empty_list_applies_to_everyone() =>
        Assert.True(TravellerApplicability.AppliesToEveryone(Array.Empty<TravellerId>()));

    [Fact]
    public void Non_empty_list_does_not_apply_to_everyone() =>
        Assert.False(TravellerApplicability.AppliesToEveryone(new[] { TravellerId.New() }));

    [Fact]
    public void AppliesTo_returns_true_for_everyone_when_list_is_empty() =>
        Assert.True(TravellerApplicability.AppliesTo(Array.Empty<TravellerId>(), TravellerId.New()));

    [Fact]
    public void AppliesTo_returns_true_when_traveller_is_in_the_list()
    {
        var id = TravellerId.New();
        Assert.True(TravellerApplicability.AppliesTo(new[] { id, TravellerId.New() }, id));
    }

    [Fact]
    public void AppliesTo_returns_false_when_traveller_is_not_in_a_non_empty_list() =>
        Assert.False(TravellerApplicability.AppliesTo(new[] { TravellerId.New() }, TravellerId.New()));

    [Fact]
    public void Intersects_is_true_when_applicable_side_is_empty()
    {
        var t = TravellerId.New();
        Assert.True(TravellerApplicability.Intersects(Array.Empty<TravellerId>(), new[] { t }));
        // empty-applicable means "applies to everyone", so it intersects even an empty candidate set.
        Assert.True(TravellerApplicability.Intersects(Array.Empty<TravellerId>(), Array.Empty<TravellerId>()));
    }

    [Fact]
    public void Intersects_is_false_when_only_the_candidate_side_is_empty()
    {
        // Non-empty applicable against no candidates has nothing to hit - explicitly false so
        // callers must short-circuit "no candidates" before delegating to Intersects if they mean
        // "everyone".
        Assert.False(TravellerApplicability.Intersects(new[] { TravellerId.New() }, Array.Empty<TravellerId>()));
    }

    [Fact]
    public void Intersects_is_true_when_lists_share_an_id()
    {
        var shared = TravellerId.New();
        Assert.True(TravellerApplicability.Intersects(
            new[] { shared, TravellerId.New() },
            new[] { TravellerId.New(), shared }));
    }

    [Fact]
    public void Intersects_is_false_for_disjoint_non_empty_lists() =>
        Assert.False(TravellerApplicability.Intersects(
            new[] { TravellerId.New() }, new[] { TravellerId.New() }));
}

public sealed class TripTravellerFilterEvaluatorTests
{
    [Fact]
    public void Everyone_mode_always_visible_regardless_of_applicability() =>
        Assert.True(TripTravellerFilterEvaluator.IsVisible(
            TravellerFilterMode.Everyone, Array.Empty<TravellerId>(), new[] { TravellerId.New() }));

    [Fact]
    public void Everyone_applicable_visible_under_Me_and_Selected()
    {
        var me = TravellerId.New();
        Assert.True(TripTravellerFilterEvaluator.IsVisible(
            TravellerFilterMode.Me, new[] { me }, Array.Empty<TravellerId>()));
        Assert.True(TripTravellerFilterEvaluator.IsVisible(
            TravellerFilterMode.Selected, new[] { me }, Array.Empty<TravellerId>()));
    }

    [Fact]
    public void Me_mode_visible_only_when_caller_is_in_the_applicable_list()
    {
        var me = TravellerId.New();
        var other = TravellerId.New();
        Assert.True(TripTravellerFilterEvaluator.IsVisible(TravellerFilterMode.Me, new[] { me }, new[] { me }));
        Assert.False(TripTravellerFilterEvaluator.IsVisible(TravellerFilterMode.Me, new[] { me }, new[] { other }));
    }

    [Fact]
    public void Selected_mode_visible_when_lists_intersect()
    {
        var a = TravellerId.New(); var b = TravellerId.New(); var c = TravellerId.New();
        Assert.True(TripTravellerFilterEvaluator.IsVisible(TravellerFilterMode.Selected, new[] { a, b }, new[] { b, c }));
        Assert.False(TripTravellerFilterEvaluator.IsVisible(TravellerFilterMode.Selected, new[] { a }, new[] { c }));
    }
}

public sealed class TripTravellerFilterTests
{
    [Fact]
    public void CreateDefault_gives_Everyone_mode_and_empty_selection()
    {
        var f = TripTravellerFilter.CreateDefault(TripId.New(), MembershipId.New());
        Assert.Equal(TravellerFilterMode.Everyone, f.Mode);
        Assert.Empty(f.SelectedTravellerIds);
    }

    [Fact]
    public void Update_clears_selection_when_mode_is_not_Selected()
    {
        var f = TripTravellerFilter.CreateDefault(TripId.New(), MembershipId.New());
        f.Update(TravellerFilterMode.Selected, new[] { TravellerId.New() });
        Assert.NotEmpty(f.SelectedTravellerIds);
        f.Update(TravellerFilterMode.Me, null);
        Assert.Empty(f.SelectedTravellerIds);
    }
}
