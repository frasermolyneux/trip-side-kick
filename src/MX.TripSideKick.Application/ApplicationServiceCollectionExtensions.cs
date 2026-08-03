using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MX.TripSideKick.Application.Trips;

namespace MX.TripSideKick.Application;

/// <summary>
/// Composition root for the application layer.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers feature-oriented application services. Repositories are registered separately by
    /// the infrastructure layer so that the application layer never binds to a storage technology.
    /// </summary>
    public static IServiceCollection AddTripSideKickApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TripCatalogService>();

        return services;
    }
}
