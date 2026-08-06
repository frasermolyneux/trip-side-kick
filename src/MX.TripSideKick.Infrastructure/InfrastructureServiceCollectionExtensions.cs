using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MX.TripSideKick.Application.Common;
using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Application.Trips;
using MX.TripSideKick.Infrastructure.HealthChecks;
using MX.TripSideKick.Infrastructure.Notifications;
using MX.TripSideKick.Infrastructure.Options;
using MX.TripSideKick.Infrastructure.Persistence;
using MX.TripSideKick.Infrastructure.Persistence.Repositories;
using MX.TripSideKick.Infrastructure.Storage;

namespace MX.TripSideKick.Infrastructure;

/// <summary>
/// Composition root for the infrastructure layer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers persistence and storage services.
    /// </summary>
    /// <remarks>
    /// SQL-backed implementations are only used when a connection string is configured; otherwise
    /// callers transparently get the in-memory "empty" implementations. That decision is resolved
    /// lazily (from <see cref="IOptions{SqlOptions}"/> at DI-resolution time), not eagerly from
    /// <paramref name="configuration"/> here - see the inline remarks below for why. Environments
    /// without a connection string (e.g. local dev without docker-compose, or a deploy slice before
    /// the database is wired up) keep startup and <c>/api/health/ready</c> working without ever
    /// touching SQL.
    /// </remarks>
    public static IServiceCollection AddTripSideKickInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SqlOptions>().Bind(configuration.GetSection(SqlOptions.SectionName));
        services.AddOptions<BlobStorageOptions>().Bind(configuration.GetSection(BlobStorageOptions.SectionName));

        services.AddMemoryCache();
        services.TryAddSingleton<BlobStorageClientFactory>();
        services.TryAddScoped<IInvitationNotifier, LoggingInvitationNotifier>();

        // Whether SQL persistence is enabled is resolved lazily from IOptions<SqlOptions> at
        // DI-resolution time, rather than read eagerly from IConfiguration here. This matters for
        // testability: WebApplicationFactory-based tests (see TripSideKickApplicationFactory)
        // inject a connection-string override via ConfigureAppConfiguration, which only lands in
        // the final IConfiguration once the host finishes building - after this method (which runs
        // from Program.cs before builder.Build()) has already executed. An eager read here would
        // therefore always see "unconfigured", regardless of test overrides. Binding via .Bind()
        // above and resolving IOptions<SqlOptions> lazily (below) means every consumer -
        // repositories, the unit of work, the DbContext, and the readiness health check - sees
        // whatever connection string is actually in effect once the host is fully built.
        services.AddDbContext<TripSideKickDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IOptions<SqlOptions>>().Value.ConnectionString;
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.TryAddScoped<ITripRepository>(serviceProvider => IsSqlConfigured(serviceProvider)
            ? ActivatorUtilities.CreateInstance<SqlTripRepository>(serviceProvider)
            : new EmptyTripRepository());
        services.TryAddScoped<IMembershipRepository>(serviceProvider => IsSqlConfigured(serviceProvider)
            ? ActivatorUtilities.CreateInstance<SqlMembershipRepository>(serviceProvider)
            : new EmptyMembershipRepository());
        services.TryAddScoped<ITravellerRepository>(serviceProvider => IsSqlConfigured(serviceProvider)
            ? ActivatorUtilities.CreateInstance<SqlTravellerRepository>(serviceProvider)
            : new EmptyTravellerRepository());
        services.TryAddScoped<IInvitationRepository>(serviceProvider => IsSqlConfigured(serviceProvider)
            ? ActivatorUtilities.CreateInstance<SqlInvitationRepository>(serviceProvider)
            : new EmptyInvitationRepository());
        services.TryAddScoped<IUnitOfWork>(serviceProvider => IsSqlConfigured(serviceProvider)
            ? ActivatorUtilities.CreateInstance<EfUnitOfWork>(serviceProvider)
            : new NoOpUnitOfWork());

        // Registered unconditionally: SqlReadinessHealthCheck itself resolves IOptions<SqlOptions>
        // lazily and reports Healthy without touching SQL when no connection string is configured,
        // so startup/readiness stays safe whether or not SQL is wired up.
        services.AddHealthChecks()
            .AddCheck<SqlReadinessHealthCheck>("sql", tags: ["ready"]);

        return services;
    }

    private static bool IsSqlConfigured(IServiceProvider serviceProvider) =>
        !string.IsNullOrWhiteSpace(serviceProvider.GetRequiredService<IOptions<SqlOptions>>().Value.ConnectionString);
}
