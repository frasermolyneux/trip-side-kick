using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MX.TripSideKick.Application.Trips;
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

        var connectionString = configuration.GetSection(SqlOptions.SectionName)[nameof(SqlOptions.ConnectionString)];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // TODO(data-slice): remove once managed-identity SQL access is wired up.
            services.TryAddScoped<ITripRepository, EmptyTripRepository>();
            return services;
        }

        services.AddDbContext<TripSideKickDbContext>(options => options.UseSqlServer(connectionString));
        services.TryAddScoped<ITripRepository, SqlTripRepository>();

        return services;
    }
}
