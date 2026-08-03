using Moq;

using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Web.Tests.Trips;

public sealed class TripCatalogServiceTests
{
    [Fact]
    public async Task ListTripsAsync_delegates_to_the_repository()
    {
        var trip = Trip.Create("Lisbon", new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 8));
        var repository = new Mock<ITripRepository>();
        repository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([trip]);

        var service = new TripCatalogService(repository.Object);

        var trips = await service.ListTripsAsync();

        Assert.Single(trips);
        Assert.Equal("Lisbon", trips[0].Name);
        repository.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTripAsync_returns_null_when_the_repository_has_no_match()
    {
        var repository = new Mock<ITripRepository>();
        repository
            .Setup(r => r.GetAsync(It.IsAny<TripId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trip?)null);

        var service = new TripCatalogService(repository.Object);

        Assert.Null(await service.GetTripAsync(TripId.New()));
    }

    [Fact]
    public void Trip_rejects_an_end_date_before_the_start_date() =>
        Assert.Throws<ArgumentException>(() =>
            Trip.Create("Bad", new LocalDate(2026, 4, 8), new LocalDate(2026, 4, 1)));
}
