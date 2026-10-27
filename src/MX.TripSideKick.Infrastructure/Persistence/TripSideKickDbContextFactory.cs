using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Offline migration authoring; applying migrations requires an explicit connection.</summary>
public sealed class TripSideKickDbContextFactory : IDesignTimeDbContextFactory<TripSideKickDbContext>
{
    public const string ConnectionStringEnvironmentVariableName = "TRIPSIDEKICK_MIGRATION_CONNECTION_STRING";

    public TripSideKickDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName);
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(string.IsNullOrWhiteSpace(connectionString)
                ? "Host=localhost;Database=tripsidekick;Username=tripsidekick"
                : connectionString)
        {
            GssEncryptionMode = Npgsql.GssEncryptionMode.Disable
        };
        var options = new DbContextOptionsBuilder<TripSideKickDbContext>().UseNpgsql(connection.ConnectionString);
        return new TripSideKickDbContext(options.Options);
    }
}
