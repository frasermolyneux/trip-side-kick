using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Trip Side Kick relational model: trips, membership/roles, minimal
/// travellers, and invitations - Journey 1 ("start a trip") and Journey 2 ("plan together").
/// </summary>
public class TripSideKickDbContext(DbContextOptions<TripSideKickDbContext> options) : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Traveller> Travellers => Set<Traveller>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TripSideKickDbContext).Assembly);
    }
}
