using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// Relational mapping for the <see cref="Invitation"/> aggregate.
/// </summary>
public sealed class InvitationEntityTypeConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, value => new InvitationId(value))
            .ValueGeneratedNever();

        builder.Property(i => i.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(i => i.InvitedEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(i => i.Role)
            .HasConversion<int>();

        builder.Property(i => i.Status)
            .HasConversion<int>();

        builder.Property(i => i.LinkKind)
            .HasConversion<int>();

        builder.Property(i => i.ExistingTravellerId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new TravellerId(value.Value) : null);

        builder.Property(i => i.NewTravellerDisplayName)
            .HasMaxLength(200);

        builder.Property(i => i.AcceptanceToken)
            .IsRequired();

        builder.Property(i => i.CreatedAtUtc)
            .HasConversion(NodaTimeConversions.InstantConverter);

        builder.Property(i => i.AcceptedAtUtc)
            .HasConversion(NodaTimeConversions.NullableInstantConverter);

        builder.Property(i => i.RowVersion)
            .IsRowVersion();

        builder.HasIndex(i => i.TripId);
        builder.HasIndex(i => i.AcceptanceToken).IsUnique();
    }
}
