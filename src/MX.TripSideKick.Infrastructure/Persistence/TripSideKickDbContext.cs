using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Domain.Trips;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Trip Side Kick relational model.
/// </summary>
/// <remarks>
/// Skeleton only in this slice: the context is registered but nothing resolves it at startup and
/// no migration has been generated yet. Readiness deliberately does not probe SQL — see
/// <c>docs/architecture-overview.md</c>.
/// </remarks>
public class TripSideKickDbContext(DbContextOptions<TripSideKickDbContext> options) : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TripSideKickDbContext).Assembly);
    }
}
