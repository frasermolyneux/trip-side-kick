using Microsoft.EntityFrameworkCore;

using DotNet.Testcontainers.Builders;

using MX.TripSideKick.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>Real PostgreSQL 18, supplied by CI or a disposable local container; never skipped.</summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable("TRIPSIDEKICK_TEST_POSTGRES") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            container = new PostgreSqlBuilder("postgres:18-bookworm")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "postgres"))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            ConnectionString = container.GetConnectionString();
        }

        ConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
        {
            GssEncryptionMode = Npgsql.GssEncryptionMode.Disable,
            SslMode = Npgsql.SslMode.Disable
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<TripSideKickDbContext>().UseNpgsql(ConnectionString);
        await using var database = new TripSideKickDbContext(options.Options);
        await DatabaseInitializer.InitializeAsync(database).ConfigureAwait(false);
        await DatabaseInitializer.InitializeAsync(database).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerTestGroup : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "PostgreSQL integration";
}
