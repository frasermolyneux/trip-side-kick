using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Trips;

/// <summary>
/// Application service for Journey 1 ("start a trip") and the trip-content parts of Journey 2.
/// </summary>
/// <remarks>
/// Controllers stay thin and call into services like this one; there is deliberately no
/// MediatR/CQRS indirection in this codebase.
/// </remarks>
public sealed class TripPlanningService(
    ITripRepository tripRepository,
    IMembershipRepository membershipRepository,
    ITravellerRepository travellerRepository,
    MembershipAccessService membershipAccess,
    IUnitOfWork unitOfWork)
{
    private readonly ITripRepository tripRepository = tripRepository ?? throw new ArgumentNullException(nameof(tripRepository));
    private readonly IMembershipRepository membershipRepository = membershipRepository ?? throw new ArgumentNullException(nameof(membershipRepository));
    private readonly ITravellerRepository travellerRepository = travellerRepository ?? throw new ArgumentNullException(nameof(travellerRepository));
    private readonly MembershipAccessService membershipAccess = membershipAccess ?? throw new ArgumentNullException(nameof(membershipAccess));
    private readonly IUnitOfWork unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    /// <summary>
    /// Creates a trip and, atomically, makes the creator its Owner and an account-linked
    /// traveller by default (they can remove themselves as a traveller afterwards without losing
    /// ownership - see <see cref="TravellerService.UnlinkSelfAsTravellerAsync"/>). Creation is
    /// online-only: it always needs a live database round trip.
    /// </summary>
    public Task<Trip> CreateTripAsync(
        CreateTripInput input, string creatorSubjectId, string creatorDisplayName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSubjectId);

        var trip = Trip.Create(input.Name, input.Destinations, input.ReportingCurrencyCode, input.Dates, input.CoverImageUrl);
        var ownerMembership = Membership.Create(trip.Id, creatorSubjectId, MembershipRole.Owner);
        var ownerTraveller = Traveller.Create(
            trip.Id,
            string.IsNullOrWhiteSpace(creatorDisplayName) ? "Trip owner" : creatorDisplayName,
            ownerMembership.Id);

        return unitOfWork.ExecuteAsync(async ct =>
        {
            await tripRepository.AddAsync(trip, ct).ConfigureAwait(false);
            await membershipRepository.AddAsync(ownerMembership, ct).ConfigureAwait(false);
            await travellerRepository.AddAsync(ownerTraveller, ct).ConfigureAwait(false);
            return trip;
        }, cancellationToken);
    }

    /// <summary>Lists every trip the subject is a member of (any role).</summary>
    public async Task<IReadOnlyList<Trip>> ListMyTripsAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        var memberships = await membershipRepository.ListForSubjectAsync(subjectId, cancellationToken).ConfigureAwait(false);
        var tripIds = memberships.Select(m => m.TripId).Distinct().ToList();

        return await tripRepository.GetManyAsync(tripIds, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets a trip the subject is a member of. Throws <see cref="NotFoundException"/> otherwise.</summary>
    public async Task<Trip> GetTripAsync(TripId tripId, string subjectId, CancellationToken cancellationToken = default)
    {
        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Viewer, cancellationToken).ConfigureAwait(false);

        return await tripRepository.GetAsync(tripId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Trip not found.");
    }

    /// <summary>
    /// Updates trip content. Requires the Editor role or higher. Enforces optimistic concurrency:
    /// throws <see cref="ConcurrencyConflictException"/> (surfaced as HTTP 409) if
    /// <paramref name="expectedRowVersion"/> no longer matches the stored value.
    /// </summary>
    public async Task<Trip> UpdateTripAsync(
        TripId tripId,
        string subjectId,
        UpdateTripInput input,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        await membershipAccess.RequireRoleAsync(tripId, subjectId, MembershipRole.Editor, cancellationToken).ConfigureAwait(false);

        var trip = await tripRepository.GetAsync(tripId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Trip not found.");

        if (input.Name is not null)
        {
            trip.Rename(input.Name);
        }

        if (input.Destinations is not null)
        {
            trip.SetDestinations(input.Destinations);
        }

        if (input.ReportingCurrencyCode is not null)
        {
            trip.SetReportingCurrency(input.ReportingCurrencyCode);
        }

        if (input.Dates is { } dates)
        {
            trip.SetDates(dates);
        }

        if (input.CoverImageUrl is not null)
        {
            trip.SetCoverImage(input.CoverImageUrl);
        }

        await tripRepository.UpdateAsync(trip, expectedRowVersion, cancellationToken).ConfigureAwait(false);
        return trip;
    }
}
