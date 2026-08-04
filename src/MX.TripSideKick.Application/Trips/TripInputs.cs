using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Application.Trips;

/// <summary>
/// Input for <see cref="TripPlanningService.CreateTripAsync"/>. Name is the only required field;
/// everything else stays <c>null</c>/empty when the creator skips it rather than being defaulted.
/// </summary>
public sealed record CreateTripInput(
    string Name,
    IReadOnlyList<string>? Destinations = null,
    string? ReportingCurrencyCode = null,
    TripDates? Dates = null,
    string? CoverImageUrl = null);

/// <summary>Input for <see cref="TripPlanningService.UpdateTripAsync"/>. Every field is optional: omitted fields are left unchanged.</summary>
public sealed record UpdateTripInput(
    string? Name = null,
    IReadOnlyList<string>? Destinations = null,
    string? ReportingCurrencyCode = null,
    TripDates? Dates = null,
    string? CoverImageUrl = null);
