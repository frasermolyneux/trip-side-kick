using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Neutral no-op implementation used when SQL is not configured.</summary>
public sealed class EmptyTripTravellerFilterRepository : ITripTravellerFilterRepository
{
    public Task<TripTravellerFilter?> GetForTripAndMembershipAsync(TripId tripId, MembershipId membershipId, CancellationToken cancellationToken = default) =>
        Task.FromResult<TripTravellerFilter?>(null);

    public Task AddAsync(TripTravellerFilter filter, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Traveller filter storage is not configured. Set 'Sql:ConnectionString' to enable it.");

    public Task UpdateAsync(TripTravellerFilter filter, byte[] expectedRowVersion, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Traveller filter storage is not configured. Set 'Sql:ConnectionString' to enable it.");
}
