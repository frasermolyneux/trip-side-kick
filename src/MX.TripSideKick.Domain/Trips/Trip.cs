using MX.TripSideKick.Domain.Common;

namespace MX.TripSideKick.Domain.Trips;

/// <summary>
/// Trip aggregate root: Journey 1 ("start a trip"). Name is the only required field; everything
/// else (destinations, reporting currency, dates, cover) is optional and stays explicitly
/// incomplete rather than being backfilled with a fabricated default. Membership (who owns/edits/
/// views the trip) and travellers are separate aggregates keyed by <see cref="Trips.TripId"/> -
/// see <c>Domain.Memberships.Membership</c> and <c>Domain.Travellers.Traveller</c>.
/// </summary>
public sealed class Trip
{
    private readonly List<string> destinations = [];

    private Trip()
    {
    }

    public TripId Id { get; private init; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional destination names. Empty means "not decided yet".</summary>
    public IReadOnlyList<string> Destinations => destinations;

    /// <summary>Optional ISO 4217 reporting currency. No FX conversion happens in this slice.</summary>
    public string? ReportingCurrencyCode { get; private set; }

    /// <summary>
    /// Dates modelled with an explicit status rather than bare nullable dates - see
    /// <see cref="TripDates"/>.
    /// </summary>
    public TripDates Dates { get; private set; } = TripDates.Undecided();

    /// <summary>Optional cover image URL (blob storage reference). No asset pipeline in this slice.</summary>
    public string? CoverImageUrl { get; private set; }

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    public static Trip Create(
        string name,
        IEnumerable<string>? destinations = null,
        string? reportingCurrencyCode = null,
        TripDates? dates = null,
        string? coverImageUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateCurrency(reportingCurrencyCode);

        var trip = new Trip
        {
            Id = TripId.New(),
            Name = name,
            ReportingCurrencyCode = NormalizeCurrency(reportingCurrencyCode),
            Dates = dates ?? TripDates.Undecided(),
            CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl
        };

        if (destinations is not null)
        {
            trip.SetDestinations(destinations);
        }

        return trip;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void SetDestinations(IEnumerable<string> newDestinations)
    {
        ArgumentNullException.ThrowIfNull(newDestinations);

        destinations.Clear();
        destinations.AddRange(newDestinations
            .Where(destination => !string.IsNullOrWhiteSpace(destination))
            .Select(destination => destination.Trim()));
    }

    public void SetReportingCurrency(string? currencyCode)
    {
        ValidateCurrency(currencyCode);
        ReportingCurrencyCode = NormalizeCurrency(currencyCode);
    }

    public void SetDates(TripDates dates) => Dates = dates;

    public void SetCoverImage(string? coverImageUrl) =>
        CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl;

    private static void ValidateCurrency(string? currencyCode)
    {
        if (currencyCode is null)
        {
            return;
        }

        if (!IsoCurrencyCodes.IsValid(currencyCode))
        {
            throw new ArgumentException(
                $"'{currencyCode}' is not a recognised ISO 4217 currency code.", nameof(currencyCode));
        }
    }

    private static string? NormalizeCurrency(string? currencyCode) =>
        string.IsNullOrWhiteSpace(currencyCode) ? null : currencyCode.ToUpperInvariant();
}
