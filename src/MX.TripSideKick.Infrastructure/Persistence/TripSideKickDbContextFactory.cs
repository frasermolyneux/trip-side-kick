using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef migrations</c> tooling to construct <see cref="TripSideKickDbContext"/>
/// at design time, without needing a running app host or a reachable database. The connection
/// string here is never used at runtime - it only has to be syntactically valid for the SQL
/// Server provider to generate migrations from the model.
/// </summary>
public sealed class TripSideKickDbContextFactory : IDesignTimeDbContextFactory<TripSideKickDbContext>
{
    public TripSideKickDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TripSideKickDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=TripSideKick;User Id=sa;Password=Design-Time-Only!;TrustServerCertificate=true;");

        return new TripSideKickDbContext(optionsBuilder.Options);
    }
}
