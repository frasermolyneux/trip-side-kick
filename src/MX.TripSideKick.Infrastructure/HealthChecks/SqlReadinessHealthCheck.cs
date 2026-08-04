using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using MX.TripSideKick.Infrastructure.Persistence;

namespace MX.TripSideKick.Infrastructure.HealthChecks;

/// <summary>
/// Optional readiness check that pings SQL. Tagged <c>ready</c> so it only ever affects
/// <c>/api/health/ready</c> - never <c>/api/health/live</c> - and degrades to
/// <see cref="HealthStatus.Degraded"/> rather than throwing/crashing startup if SQL is briefly
/// unavailable (e.g. serverless auto-pause resuming).
/// </summary>
public sealed class SqlReadinessHealthCheck(TripSideKickDbContext dbContext) : IHealthCheck
{
    private readonly TripSideKickDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy("SQL is reachable.")
                : HealthCheckResult.Degraded("SQL is not currently reachable.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Never let a transient SQL problem (e.g. serverless auto-pause resuming) take the
            // whole readiness probe down with an unhandled exception.
            return HealthCheckResult.Degraded("SQL is not currently reachable.", exception);
        }
    }
}
