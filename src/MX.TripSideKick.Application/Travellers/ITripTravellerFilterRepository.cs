using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Travellers;

/// <summary>Persistence boundary for a member's persistent trip-scoped <see cref="TripTravellerFilter"/>.</summary>
public interface ITripTravellerFilterRepository
{
    Task<TripTravellerFilter?> GetForTripAndMembershipAsync(
        TripId tripId, MembershipId membershipId, CancellationToken cancellationToken = default);

    Task AddAsync(TripTravellerFilter filter, CancellationToken cancellationToken = default);

    Task UpdateAsync(TripTravellerFilter filter, byte[] expectedRowVersion, CancellationToken cancellationToken = default);
}
