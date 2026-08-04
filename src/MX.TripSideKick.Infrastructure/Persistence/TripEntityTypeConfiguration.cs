using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// Relational mapping for the <see cref="Trip"/> aggregate.
/// </summary>
public sealed class TripEntityTypeConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Trips");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new TripId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.ReportingCurrencyCode)
            .HasMaxLength(3);

        builder.Property(t => t.CoverImageUrl)
            .HasMaxLength(2048);

        // Destinations is a simple optional string list (no per-destination metadata in this
        // slice) - stored as JSON rather than a delimited string so destination names can contain
        // any character without collision risk.
        var destinationsComparer = new ValueComparer<IReadOnlyList<string>>(
            (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
            value => value.Aggregate(0, (hash, destination) => HashCode.Combine(hash, destination.GetHashCode(StringComparison.Ordinal))),
            value => value.ToList());

        builder.Property(t => t.Destinations)
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default),
                value => JsonSerializer.Deserialize<List<string>>(value, JsonSerializerOptions.Default) ?? new List<string>())
            .Metadata.SetValueComparer(destinationsComparer);

        // TripDates is a value object (readonly record struct) mapped as a complex property so it
        // has no identity of its own and always lives inline on the Trips row.
        builder.ComplexProperty(t => t.Dates, dates =>
        {
            dates.Property(d => d.Status).HasColumnName("DateStatus");
            dates.Property(d => d.StartDate).HasColumnName("StartDate").HasConversion(NodaTimeConversions.NullableLocalDateConverter);
            dates.Property(d => d.EndDate).HasColumnName("EndDate").HasConversion(NodaTimeConversions.NullableLocalDateConverter);
        });

        // rowversion backs both optimistic concurrency and HTTP ETags on the API surface.
        builder.Property(t => t.RowVersion)
            .IsRowVersion();
    }
}
