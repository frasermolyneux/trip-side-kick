using Microsoft.EntityFrameworkCore;

namespace MX.TripSideKick.Infrastructure.Persistence;

/// <summary>Explicit, retryable initialization. EF's PostgreSQL migration lock serializes runners.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(TripSideKickDbContext database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        await database.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
