using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Itinerary;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Trip Side Kick relational model: trips, membership/roles, minimal
/// travellers, invitations - and (Journey 5) itinerary items, comments, activity feed, and per-
/// member persistent traveller filters.
/// </summary>
public class TripSideKickDbContext(DbContextOptions<TripSideKickDbContext> options) : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Traveller> Travellers => Set<Traveller>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();

    public DbSet<ItineraryComment> ItineraryComments => Set<ItineraryComment>();

    public DbSet<TripActivityFeedEntry> TripActivityFeedEntries => Set<TripActivityFeedEntry>();

    public DbSet<TripTravellerFilter> TripTravellerFilters => Set<TripTravellerFilter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TripSideKickDbContext).Assembly);
    }
}
