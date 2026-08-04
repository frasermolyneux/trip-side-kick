using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence.Repositories;

/// <summary>Empty repository used when no SQL connection string is configured.</summary>
public sealed class EmptyInvitationRepository : IInvitationRepository
{
    public Task<Invitation?> GetAsync(InvitationId id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Invitation?>(null);

    public Task<Invitation?> GetByAcceptanceTokenAsync(Guid acceptanceToken, CancellationToken cancellationToken = default) =>
        Task.FromResult<Invitation?>(null);

    public Task<IReadOnlyList<Invitation>> ListForTripAsync(TripId tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Invitation>>([]);

    public Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task UpdateAsync(Invitation invitation, byte[] expectedRowVersion, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    private static InvalidOperationException NotConfigured() =>
        new("Invitation persistence is not configured. Set the 'Sql:ConnectionString' setting to enable it.");
}
