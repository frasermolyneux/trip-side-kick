using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Travellers;

/// <summary>
/// Persistence boundary for the <see cref="Traveller"/> aggregate.
/// </summary>
public interface ITravellerRepository
{
    Task<Traveller?> GetAsync(TravellerId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Traveller>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default);

    Task<Traveller?> GetLinkedToMembershipAsync(MembershipId membershipId, CancellationToken cancellationToken = default);

    Task AddAsync(Traveller traveller, CancellationToken cancellationToken = default);

    Task UpdateAsync(Traveller traveller, byte[] expectedRowVersion, CancellationToken cancellationToken = default);

    Task RemoveAsync(Traveller traveller, CancellationToken cancellationToken = default);
}
