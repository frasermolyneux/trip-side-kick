using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Relational mapping for the <see cref="ItineraryItem"/> aggregate.</summary>
public sealed class ItineraryItemEntityTypeConfiguration : IEntityTypeConfiguration<ItineraryItem>
{
    public void Configure(EntityTypeBuilder<ItineraryItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ItineraryItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new ItineraryItemId(value))
            .ValueGeneratedNever();

        builder.Property(i => i.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(ItineraryItem.MaxTitleLength);

        builder.Property(i => i.Notes);
        builder.Property(i => i.Location).HasMaxLength(500);

        // ItinerarySchedule is a value object (readonly record struct) mapped as a complex property
        // so it has no identity of its own and always lives inline on the ItineraryItems row -
        // same pattern as TripDates on Trips.
        builder.ComplexProperty(i => i.Schedule, schedule =>
        {
            schedule.Property(s => s.Status).HasColumnName("ScheduleStatus");
            schedule.Property(s => s.Date).HasColumnName("ScheduledDate").HasConversion(NodaTimeConversions.NullableLocalDateConverter);
            schedule.Property(s => s.StartTime).HasColumnName("ScheduledStartTime").HasConversion(NodaTimeConversions.NullableLocalTimeConverter);
            schedule.Property(s => s.EndTime).HasColumnName("ScheduledEndTime").HasConversion(NodaTimeConversions.NullableLocalTimeConverter);
        });

        // ApplicableTravellerIds stored as JSON so a single "empty list = everyone" column stays
        // simple and predictable; same value-comparer pattern as Trip.Destinations.
        var applicabilityComparer = new ValueComparer<IReadOnlyList<TravellerId>>(
            (left, right) => (left ?? new List<TravellerId>()).SequenceEqual(right ?? new List<TravellerId>()),
            value => value.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.Value.GetHashCode())),
            value => value.ToList());

        builder.Property(i => i.ApplicableTravellerIds)
            .HasColumnName("ApplicableTravellerIds")
            .HasConversion(
                value => JsonSerializer.Serialize(value.Select(id => id.Value).ToList(), JsonSerializerOptions.Default),
                value => (JsonSerializer.Deserialize<List<Guid>>(value, JsonSerializerOptions.Default) ?? new List<Guid>())
                    .Select(g => new TravellerId(g)).ToList())
            .Metadata.SetValueComparer(applicabilityComparer);

        builder.Property(i => i.RowVersion)
            .IsRowVersion();

        builder.HasIndex(i => i.TripId);
    }
}
