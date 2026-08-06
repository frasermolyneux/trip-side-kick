using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Empty repository used when no SQL connection string is configured.</summary>
public sealed class EmptyTravellerRepository : ITravellerRepository
{
    public Task<Traveller?> GetAsync(TravellerId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Traveller?>(null);

    public Task<IReadOnlyList<Traveller>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Traveller>>([]);

    public Task<Traveller?> GetLinkedToMembershipAsync(MembershipId membershipId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Traveller?>(null);

    public Task AddAsync(Traveller traveller, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task UpdateAsync(Traveller traveller, byte[] expectedRowVersion, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task RemoveAsync(Traveller traveller, CancellationToken cancellationToken = default) => throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("Traveller persistence is not configured. Set the 'Sql:ConnectionString' setting to enable it.");
}
