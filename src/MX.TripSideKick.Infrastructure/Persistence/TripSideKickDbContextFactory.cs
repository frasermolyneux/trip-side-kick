using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef</c> tooling (migrations authoring, and applying migrations against a
/// real database) to construct <see cref="TripSideKickDbContext"/> at design time, without
/// needing a running app host.
/// </summary>
/// <remarks>
/// When authoring migrations offline (<c>dotnet ef migrations add</c>), the connection string
/// only has to be syntactically valid for the SQL Server provider to generate the model diff - it
/// is never actually connected to. When <em>applying</em> migrations against a real, ephemeral
/// database (the Playwright E2E harness's Testcontainers SQL Server - see
/// <c>tests/e2e/global-setup.ts</c> and <c>docs/testing.md</c>), the harness sets
/// <see cref="ConnectionStringEnvironmentVariableName"/> so <c>dotnet ef database update</c>
/// targets the container instead of the hardcoded local default below.
/// </remarks>
public sealed class TripSideKickDbContextFactory : IDesignTimeDbContextFactory<TripSideKickDbContext>
{
    /// <summary>
    /// Environment variable that, when set, overrides the connection string used by
    /// <c>dotnet ef</c> tooling. Never read outside design-time/tooling contexts - the running
    /// app always resolves its connection string from <c>Sql:ConnectionString</c> configuration
    /// (see <c>InfrastructureServiceCollectionExtensions</c>), not this factory.
    /// </summary>
    public const string ConnectionStringEnvironmentVariableName = "TRIPSIDEKICK_MIGRATION_CONNECTION_STRING";

    public TripSideKickDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName);

        var optionsBuilder = new DbContextOptionsBuilder<TripSideKickDbContext>();
        optionsBuilder.UseSqlServer(string.IsNullOrWhiteSpace(connectionString)
            ? "Server=localhost;Database=TripSideKick;User Id=sa;Password=Development-Only-Placeholder1!;TrustServerCertificate=true;"
            : connectionString);

        return new TripSideKickDbContext(optionsBuilder.Options);
    }
}
