using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Relational mapping for the append-only <see cref="ItineraryComment"/> entity.</summary>
public sealed class ItineraryCommentEntityTypeConfiguration : IEntityTypeConfiguration<ItineraryComment>
{
    public void Configure(EntityTypeBuilder<ItineraryComment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ItineraryComments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new ItineraryCommentId(value))
            .ValueGeneratedNever();

        builder.Property(c => c.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(c => c.ItineraryItemId)
            .HasConversion(id => id.Value, value => new ItineraryItemId(value));

        builder.Property(c => c.AuthorSubjectId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Body)
            .IsRequired()
            .HasMaxLength(ItineraryComment.MaxBodyLength);

        builder.Property(c => c.CreatedAt)
            .HasConversion(NodaTimeConversions.InstantConverter);

        builder.HasIndex(c => c.ItineraryItemId);
        builder.HasIndex(c => c.TripId);
    }
}
