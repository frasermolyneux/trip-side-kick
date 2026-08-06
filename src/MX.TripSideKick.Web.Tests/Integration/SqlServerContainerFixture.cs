using Microsoft.EntityFrameworkCore;

using MX.TripSideKick.Infrastructure.Persistence;

using Testcontainers.MsSql;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>
/// Starts a single, hermetic SQL Server container (via Testcontainers) shared by every test class
/// in the <c>"SQL Server integration"</c> collection, applies the real EF Core migrations against
/// it once, and exposes the resulting connection string. No Azure SQL, no seeded cloud state - the
/// database only ever exists for the lifetime of the test run.
/// </summary>
/// <remarks>
/// Requires a working Docker (or Podman, via <c>TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE</c>) daemon
/// on the machine running the tests - see docs/testing.md for local/CI prerequisites. If Docker is
/// unavailable, <see cref="InitializeAsync"/> throws and every test in the collection fails with a
/// clear "container failed to start" error rather than silently skipping coverage.
/// </remarks>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync().ConfigureAwait(false);

        var optionsBuilder = new DbContextOptionsBuilder<TripSideKickDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        await using var dbContext = new TripSideKickDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await container.DisposeAsync().ConfigureAwait(false);
}

/// <summary>Groups every integration test class that needs the shared <see cref="SqlServerContainerFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerTestGroup : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SQL Server integration";
}
