using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using MX.TripSideKick.Application.Invitations;
using MX.TripSideKick.Application.Itinerary;
using MX.TripSideKick.Application.Memberships;
using MX.TripSideKick.Application.Travellers;
using MX.TripSideKick.Application.Trips;

using NodaTime;

namespace MX.TripSideKick.Application;

/// <summary>
/// Composition root for the application layer.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers feature-oriented application services. Repositories (and the unit of work) are
    /// registered separately by the infrastructure layer so that the application layer never binds
    /// to a storage technology.
    /// </summary>
    public static IServiceCollection AddTripSideKickApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IClock>(SystemClock.Instance);

        services.TryAddScoped<MembershipAccessService>();
        services.TryAddScoped<TripPlanningService>();
        services.TryAddScoped<MembershipService>();
        services.TryAddScoped<TravellerService>();
        services.TryAddScoped<InvitationService>();
        services.TryAddScoped<ItineraryPlanningService>();
        services.TryAddScoped<TripTravellerFilterService>();

        return services;
    }
}
