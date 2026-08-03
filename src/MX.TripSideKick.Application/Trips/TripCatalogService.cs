using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Trips;

/// <summary>
/// Feature-oriented application service for trip read operations.
/// </summary>
/// <remarks>
/// Controllers stay thin and call into services like this one; there is deliberately no
/// MediatR/CQRS indirection in this codebase.
/// </remarks>
public sealed class TripCatalogService(ITripRepository repository)
{
    private readonly ITripRepository repository = repository
        ?? throw new ArgumentNullException(nameof(repository));

    public Task<IReadOnlyList<Trip>> ListTripsAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(cancellationToken);

    public Task<Trip?> GetTripAsync(TripId id, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, cancellationToken);
}
