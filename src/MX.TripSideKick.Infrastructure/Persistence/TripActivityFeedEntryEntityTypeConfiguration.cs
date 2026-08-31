using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Relational mapping for the append-only <see cref="TripActivityFeedEntry"/> entity.</summary>
public sealed class TripActivityFeedEntryEntityTypeConfiguration : IEntityTypeConfiguration<TripActivityFeedEntry>
{
    public void Configure(EntityTypeBuilder<TripActivityFeedEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TripActivityFeedEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new TripActivityFeedEntryId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(e => e.ActorSubjectId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EventType)
            .HasConversion<int>();

        builder.Property(e => e.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.OccurredAt)
            .HasConversion(NodaTimeConversions.InstantConverter);

        builder.Property(e => e.ItineraryItemId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new ItineraryItemId(value.Value) : null);

        // Feed queries are always "give me this trip's most recent entries" - compound index keeps
        // that read cheap without another table scan.
        builder.HasIndex(e => new { e.TripId, e.OccurredAt });
    }
}
