using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using Moq;

namespace MX.TripSideKick.Web.Tests.Application.Travellers;

public sealed class TripTravellerFilterServiceTests
{
    private readonly Mock<ITripTravellerFilterRepository> filterRepository = new();
    private readonly Mock<ITravellerRepository> travellerRepository = new();
    private readonly Mock<IMembershipRepository> membershipRepository = new();
    private readonly TripTravellerFilterService sut;

    private readonly TripId tripId = TripId.New();
    private const string SubjectId = "user-1";
    private Membership membership;

    public TripTravellerFilterServiceTests()
    {
        membership = Membership.Create(tripId, SubjectId, MembershipRole.Viewer);
        membershipRepository
            .Setup(r => r.GetForTripAndSubjectAsync(tripId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        sut = new TripTravellerFilterService(
            filterRepository.Object,
            travellerRepository.Object,
            new MembershipAccessService(membershipRepository.Object));
    }

    [Fact]
    public async Task GetOrCreate_creates_a_default_row_on_first_read()
    {
        filterRepository
            .Setup(r => r.GetForTripAndMembershipAsync(tripId, membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TripTravellerFilter?)null);

        var filter = await sut.GetOrCreateForCallerAsync(tripId, SubjectId);

        Assert.Equal(TravellerFilterMode.Everyone, filter.Mode);
        filterRepository.Verify(r => r.AddAsync(It.IsAny<TripTravellerFilter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveEffectiveTravellerIds_returns_empty_for_Everyone()
    {
        var filter = TripTravellerFilter.CreateDefault(tripId, membership.Id);
        Assert.Empty(await sut.ResolveEffectiveTravellerIdsAsync(filter));
    }

    [Fact]
    public async Task ResolveEffectiveTravellerIds_returns_the_selected_ids_for_Selected()
    {
        var filter = TripTravellerFilter.CreateDefault(tripId, membership.Id);
        var t = TravellerId.New();
        filter.Update(TravellerFilterMode.Selected, new[] { t });

        var effective = await sut.ResolveEffectiveTravellerIdsAsync(filter);

        Assert.Contains(t, effective);
    }

    [Fact]
    public async Task ResolveEffectiveTravellerIds_looks_up_the_linked_traveller_for_Me()
    {
        var filter = TripTravellerFilter.CreateDefault(tripId, membership.Id);
        filter.Update(TravellerFilterMode.Me, null);

        var linked = Traveller.Create(tripId, "Me", membership.Id);
        travellerRepository
            .Setup(r => r.GetLinkedToMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linked);

        var effective = await sut.ResolveEffectiveTravellerIdsAsync(filter);

        Assert.Contains(linked.Id, effective);
    }

    [Fact]
    public async Task ResolveEffectiveTravellerIds_returns_empty_when_no_traveller_is_linked_and_mode_is_Me()
    {
        var filter = TripTravellerFilter.CreateDefault(tripId, membership.Id);
        filter.Update(TravellerFilterMode.Me, null);

        travellerRepository
            .Setup(r => r.GetLinkedToMembershipAsync(membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Traveller?)null);

        Assert.Empty(await sut.ResolveEffectiveTravellerIdsAsync(filter));
    }
}
