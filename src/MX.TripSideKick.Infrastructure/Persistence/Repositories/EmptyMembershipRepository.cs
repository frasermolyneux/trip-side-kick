using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Empty repository used when no SQL connection string is configured.</summary>
public sealed class EmptyMembershipRepository : IMembershipRepository
{
    public Task<Membership?> GetAsync(MembershipId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Membership?>(null);

    public Task<Membership?> GetForTripAndSubjectAsync(TripId tripId, string subjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<Membership?>(null);

    public Task<IReadOnlyList<Membership>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Membership>>([]);

    public Task<IReadOnlyList<Membership>> ListForSubjectAsync(string subjectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Membership>>([]);

    public Task AddAsync(Membership membership, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task UpdateAsync(Membership membership, byte[] expectedRowVersion, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task RemoveAsync(Membership membership, CancellationToken cancellationToken = default) => throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("Membership persistence is not configured. Set the 'Sql:ConnectionString' setting to enable it.");
}
