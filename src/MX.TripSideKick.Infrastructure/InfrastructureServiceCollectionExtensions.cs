using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// SQL-backed services are only registered when a connection string is present. The walking
    /// skeleton deploys without one, so startup and <c>/api/health/ready</c> must never depend on
    /// the database being reachable.
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

        var connectionString = configuration.GetSection(SqlOptions.SectionName)[nameof(SqlOptions.ConnectionString)];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // No SQL connection string is configured (e.g. local dev without docker-compose, or a
            // deploy slice before the database is wired up) - fall back to empty repositories so
            // DI stays valid and startup/readiness never touch SQL.
            services.TryAddScoped<ITripRepository, EmptyTripRepository>();
            services.TryAddScoped<IMembershipRepository, EmptyMembershipRepository>();
            services.TryAddScoped<ITravellerRepository, EmptyTravellerRepository>();
            services.TryAddScoped<IInvitationRepository, EmptyInvitationRepository>();
            services.TryAddScoped<IUnitOfWork, NoOpUnitOfWork>();
            return services;
        }

        services.AddDbContext<TripSideKickDbContext>(options => options.UseSqlServer(connectionString));
        services.TryAddScoped<ITripRepository, SqlTripRepository>();
        services.TryAddScoped<IMembershipRepository, SqlMembershipRepository>();
        services.TryAddScoped<ITravellerRepository, SqlTravellerRepository>();
        services.TryAddScoped<IInvitationRepository, SqlInvitationRepository>();
        services.TryAddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddHealthChecks()
            .AddCheck<SqlReadinessHealthCheck>("sql", tags: ["ready"]);

        return services;
    }
}
