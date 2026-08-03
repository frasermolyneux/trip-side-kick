using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Trips;

using NodaTime;

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

        builder.Property(t => t.StartDate)
            .HasConversion(d => d.ToDateOnly(), d => LocalDate.FromDateOnly(d));

        builder.Property(t => t.EndDate)
            .HasConversion(d => d.ToDateOnly(), d => LocalDate.FromDateOnly(d));

        // rowversion backs both optimistic concurrency and HTTP ETags on the API surface.
        builder.Property(t => t.RowVersion)
            .IsRowVersion();
    }
}
