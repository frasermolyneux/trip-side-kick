using NodaTime;

namespace MX.TripSideKick.Domain.Trips;

/// <summary>
/// Placeholder trip aggregate root. The walking skeleton only needs enough shape to prove the
/// domain / application / infrastructure boundaries; real itinerary modelling lands in a later slice.
/// </summary>
public sealed class Trip
{
    private Trip()
    {
    }

    public TripId Id { get; private init; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Local (timezone-free) start date of the trip.</summary>
    public LocalDate StartDate { get; private set; }

    /// <summary>Local (timezone-free) end date of the trip.</summary>
    public LocalDate EndDate { get; private set; }

    /// <summary>SQL <c>rowversion</c> used for optimistic concurrency and HTTP ETags.</summary>
    public byte[]? RowVersion { get; private set; }

    public static Trip Create(string name, LocalDate startDate, LocalDate endDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (endDate < startDate)
        {
            throw new ArgumentException("Trip end date cannot be before the start date.", nameof(endDate));
        }

        return new Trip
        {
            Id = TripId.New(),
            Name = name,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
