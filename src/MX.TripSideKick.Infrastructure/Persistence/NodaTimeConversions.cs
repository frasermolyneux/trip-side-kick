using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using NodaTime;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Shared EF Core value converters for NodaTime types used across entity configurations.</summary>
internal static class NodaTimeConversions
{
    public static readonly ValueConverter<LocalDate, DateOnly> LocalDateConverter =
        new(date => date.ToDateOnly(), date => LocalDate.FromDateOnly(date));

    public static readonly ValueConverter<LocalDate?, DateOnly?> NullableLocalDateConverter =
        new(
            date => date.HasValue ? date.Value.ToDateOnly() : null,
            date => date.HasValue ? LocalDate.FromDateOnly(date.Value) : null);

    public static readonly ValueConverter<Instant, DateTime> InstantConverter =
        new(instant => instant.ToDateTimeUtc(), dateTime => Instant.FromDateTimeUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)));

    public static readonly ValueConverter<Instant?, DateTime?> NullableInstantConverter =
        new(
            instant => instant.HasValue ? instant.Value.ToDateTimeUtc() : null,
            dateTime => dateTime.HasValue ? Instant.FromDateTimeUtc(DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc)) : null);
}
