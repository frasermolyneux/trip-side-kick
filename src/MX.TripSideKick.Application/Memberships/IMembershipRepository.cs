using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Memberships;

/// <summary>
/// Persistence boundary for the <see cref="Membership"/> aggregate.
/// </summary>
public interface IMembershipRepository
{
    Task<Membership?> GetAsync(MembershipId id, CancellationToken cancellationToken = default);

    Task<Membership?> GetForTripAndSubjectAsync(TripId tripId, string subjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Membership>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Membership>> ListForSubjectAsync(string subjectId, CancellationToken cancellationToken = default);

    Task AddAsync(Membership membership, CancellationToken cancellationToken = default);

    Task UpdateAsync(Membership membership, byte[] expectedRowVersion, CancellationToken cancellationToken = default);

    Task RemoveAsync(Membership membership, CancellationToken cancellationToken = default);
}
