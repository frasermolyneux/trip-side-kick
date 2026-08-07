using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Relational mapping for a member's persistent <see cref="TripTravellerFilter"/>.</summary>
public sealed class TripTravellerFilterEntityTypeConfiguration : IEntityTypeConfiguration<TripTravellerFilter>
{
    public void Configure(EntityTypeBuilder<TripTravellerFilter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TripTravellerFilters");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasConversion(id => id.Value, value => new TripTravellerFilterId(value))
            .ValueGeneratedNever();

        builder.Property(f => f.TripId)
            .HasConversion(id => id.Value, value => new TripId(value));

        builder.Property(f => f.MembershipId)
            .HasConversion(id => id.Value, value => new MembershipId(value));

        builder.Property(f => f.Mode)
            .HasConversion<int>();

        var selectedComparer = new ValueComparer<IReadOnlyList<TravellerId>>(
            (left, right) => (left ?? new List<TravellerId>()).SequenceEqual(right ?? new List<TravellerId>()),
            value => value.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.Value.GetHashCode())),
            value => value.ToList());

        builder.Property(f => f.SelectedTravellerIds)
            .HasColumnName("SelectedTravellerIds")
            .HasConversion(
                value => JsonSerializer.Serialize(value.Select(id => id.Value).ToList(), JsonSerializerOptions.Default),
                value => (JsonSerializer.Deserialize<List<Guid>>(value, JsonSerializerOptions.Default) ?? new List<Guid>())
                    .Select(g => new TravellerId(g)).ToList())
            .Metadata.SetValueComparer(selectedComparer);

        builder.Property(f => f.RowVersion)
            .IsRowVersion();

        // One filter row per (trip, membership) - the API auto-creates the default row on first
        // read and race-safely upserts it (see TripTravellerFilterService).
        builder.HasIndex(f => new { f.TripId, f.MembershipId }).IsUnique();
    }
}
