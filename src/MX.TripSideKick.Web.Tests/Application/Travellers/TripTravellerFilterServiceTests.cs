using MX.TripSideKick.Application.Common;
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

    [Fact]
    public async Task UpdateForCaller_uses_the_created_rows_RowVersion_on_first_write_auto_create()
    {
        // No row exists yet, so the caller cannot have a valid expected RowVersion. The service
        // creates the row and must then update it against the newly persisted RowVersion rather
        // than the caller's placeholder value (which would fail with a spurious 409).
        filterRepository
            .Setup(r => r.GetForTripAndMembershipAsync(tripId, membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TripTravellerFilter?)null);

        filterRepository
            .Setup(r => r.AddAsync(It.IsAny<TripTravellerFilter>(), It.IsAny<CancellationToken>()))
            .Callback<TripTravellerFilter, CancellationToken>((created, _) => WithRowVersion(created, [7, 7, 7]))
            .Returns(Task.CompletedTask);

        var updated = await sut.UpdateForCallerAsync(
            tripId, SubjectId, TravellerFilterMode.Me, null, expectedRowVersion: [1, 2, 3]);

        Assert.Equal(TravellerFilterMode.Me, updated.Mode);
        filterRepository.Verify(
            r => r.UpdateAsync(updated, new byte[] { 7, 7, 7 }, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateForCaller_recovers_when_a_concurrent_first_write_creates_the_row_first()
    {
        // The caller has never read the filter (no row exists from their point of view), so they
        // send an update with a made-up/empty expected RowVersion. Concurrently, another request
        // wins the race to create the row, so this caller's AddAsync throws a concurrency
        // conflict. The service must not lose the caller's requested mode/selection - it should
        // re-read the winning row and apply the update against its real RowVersion instead of
        // propagating the conflict.
        filterRepository
            .SetupSequence(r => r.GetForTripAndMembershipAsync(tripId, membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TripTravellerFilter?)null)
            .ReturnsAsync(WithRowVersion(TripTravellerFilter.CreateDefault(tripId, membership.Id), [9, 9, 9]));

        filterRepository
            .Setup(r => r.AddAsync(It.IsAny<TripTravellerFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Another request created the row first."));

        var selected = TravellerId.New();
        var updated = await sut.UpdateForCallerAsync(
            tripId, SubjectId, TravellerFilterMode.Selected, new[] { selected }, expectedRowVersion: [1, 2, 3]);

        Assert.Equal(TravellerFilterMode.Selected, updated.Mode);
        Assert.Contains(selected, updated.SelectedTravellerIds);
        // Must use the re-read row's own RowVersion, not the caller's stale/made-up expected value.
        filterRepository.Verify(
            r => r.UpdateAsync(updated, new byte[] { 9, 9, 9 }, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreate_throws_if_concurrent_create_conflict_occurs_and_re_read_still_returns_null()
    {
        filterRepository
            .SetupSequence(r => r.GetForTripAndMembershipAsync(tripId, membership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TripTravellerFilter?)null)
            .ReturnsAsync((TripTravellerFilter?)null);

        filterRepository
            .Setup(r => r.AddAsync(It.IsAny<TripTravellerFilter>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("Another request created the row first."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetOrCreateForCallerAsync(tripId, SubjectId));

        Assert.Equal(
            "Traveller filter row disappeared immediately after a concurrency conflict.",
            ex.Message);
    }

    private static TripTravellerFilter WithRowVersion(TripTravellerFilter filter, byte[] rowVersion)
    {
        typeof(TripTravellerFilter)
            .GetProperty(nameof(TripTravellerFilter.RowVersion))!
            .SetValue(filter, rowVersion);
        return filter;
    }
}
