using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using MX.TripSideKick.Infrastructure.Options;
using MX.TripSideKick.Infrastructure.Persistence;

namespace MX.TripSideKick.Infrastructure.HealthChecks;

/// <summary>
/// Optional readiness check that pings SQL. Tagged <c>ready</c> so it only ever affects
/// <c>/api/health/ready</c> - never <c>/api/health/live</c> - and degrades to
/// <see cref="HealthStatus.Degraded"/> rather than throwing/crashing startup if SQL is briefly
/// unavailable (e.g. serverless auto-pause resuming).
/// </summary>
/// <remarks>
/// Registered unconditionally by <c>AddTripSideKickInfrastructure</c>. This check resolves
/// <see cref="SqlOptions"/> lazily and reports <see cref="HealthStatus.Healthy"/> without ever
/// resolving <see cref="TripSideKickDbContext"/> when no connection string is configured - the
/// <see cref="TripSideKickDbContext"/> registration has no provider configured in that case, so
/// resolving it would throw.
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
            return HealthCheckResult.Healthy("SQL is not configured; readiness check skipped.");
        }

        try
        {
            var dbContext = serviceProvider.GetRequiredService<TripSideKickDbContext>();
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
