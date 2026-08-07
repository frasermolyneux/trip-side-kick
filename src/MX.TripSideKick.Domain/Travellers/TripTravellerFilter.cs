using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Domain.Travellers;

/// <summary>
/// A single member's persistent trip-scoped preference for which travellers' items they want to
/// see. One row per (<see cref="TripId"/>, <see cref="MembershipId"/>) pair, enforced by a unique
/// index in the infrastructure layer.
/// </summary>
/// <remarks>
/// Consumed by the itinerary surface in this slice via
/// <see cref="TripTravellerFilterEvaluator.IsVisible"/>; any future surface (calendar view, packing
/// list, Today screen, offline snapshot) can reuse the same evaluator against its own
/// applicable-traveller-id list without re-implementing the Everyone/Me/Selected fan-out.
/// </remarks>
public sealed class TripTravellerFilter
{
    private readonly List<TravellerId> selectedTravellerIds = [];

    private TripTravellerFilter()
    {
    }

    public TripTravellerFilterId Id { get; private init; }

    public TripId TripId { get; private init; }

    public MembershipId MembershipId { get; private init; }

    public TravellerFilterMode Mode { get; private set; }

    /// <summary>
    /// Only meaningful when <see cref="Mode"/> is <see cref="TravellerFilterMode.Selected"/>; kept
    /// empty otherwise so the JSON column stays predictable.
    /// </summary>
    public IReadOnlyList<TravellerId> SelectedTravellerIds => selectedTravellerIds;

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    public static TripTravellerFilter CreateDefault(TripId tripId, MembershipId membershipId) =>
        new()
        {
            Id = TripTravellerFilterId.New(),
            TripId = tripId,
            MembershipId = membershipId,
            Mode = TravellerFilterMode.Everyone
        };

    public void Update(TravellerFilterMode mode, IEnumerable<TravellerId>? selectedTravellerIds)
    {
        Mode = mode;
        this.selectedTravellerIds.Clear();

        if (mode == TravellerFilterMode.Selected && selectedTravellerIds is not null)
        {
            this.selectedTravellerIds.AddRange(selectedTravellerIds.Distinct());
        }
    }
}
