using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using MX.TripSideKick.Infrastructure.Options;
using MX.TripSideKick.Infrastructure.Persistence;

namespace MX.TripSideKick.Infrastructure.HealthChecks;

/// <summary>
/// Checks PostgreSQL connectivity and migration state. Missing configuration, pending schema
/// changes and connection failures are unhealthy without affecting liveness.
/// </summary>
/// <remarks>
/// Options resolve lazily so final runtime and test overrides are honored.
/// </remarks>
public sealed class SqlReadinessHealthCheck(IOptions<SqlOptions> sqlOptions, IServiceProvider serviceProvider) : IHealthCheck
{
    private readonly IOptions<SqlOptions> sqlOptions = sqlOptions ?? throw new ArgumentNullException(nameof(sqlOptions));
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sqlOptions.Value.ConnectionString))
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not configured.");
        }

        try
        {
            var dbContext = serviceProvider.GetRequiredService<TripSideKickDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            var pending = canConnect
                ? await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)
                : ["unreachable"];
            return canConnect && !pending.Any()
                ? HealthCheckResult.Healthy("SQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL is not currently reachable.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not currently reachable.");
        }
    }
}
