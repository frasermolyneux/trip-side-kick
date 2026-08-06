using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// Relational mapping for the <see cref="Traveller"/> aggregate.
/// </summary>
public sealed class TravellerEntityTypeConfiguration : IEntityTypeConfiguration<Traveller>
{
    public void Configure(EntityTypeBuilder<Traveller> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Travellers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new TravellerId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.LinkedMembershipId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new MembershipId(value.Value) : null);

        builder.Property(t => t.RowVersion)
            .IsRowVersion();

        // A membership can be linked to at most one traveller (enforced by the application layer
        // too, but a filtered unique index keeps it true even under concurrent writes).
        builder.HasIndex(t => t.LinkedMembershipId)
            .IsUnique()
            .HasFilter("[LinkedMembershipId] IS NOT NULL");

        builder.HasIndex(t => t.TripId);
    }
}
