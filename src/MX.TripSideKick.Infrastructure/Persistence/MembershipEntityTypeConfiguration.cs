using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// Relational mapping for the <see cref="Membership"/> aggregate.
/// </summary>
public sealed class MembershipEntityTypeConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MembershipId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(m => m.SubjectId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Role)
            .HasConversion<int>();

        builder.Property(m => m.RowVersion)
            .IsRowVersion();

        // A subject can only have one membership per trip.
        builder.HasIndex(m => new { m.TripId, m.SubjectId }).IsUnique();

        // Membership lookups by subject ("my trips") happen on every trips-list request.
        builder.HasIndex(m => m.SubjectId);
    }
}
