namespace MX.TripSideKick.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request.
/// </summary>
/// <remarks>
/// IDENTITY STUB (walking skeleton): the only implementation today is
/// <c>MX.TripSideKick.Web.Hosting.AnonymousCurrentUser</c>, which always reports a signed-out user.
/// The identity slice replaces it with a claims-backed implementation fed by Microsoft Entra
/// External ID (B2B collaboration + self-service sign-up) in the Molyneux.IO workforce tenant.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable subject identifier of the signed-in user, or <c>null</c> when anonymous.</summary>
    string? SubjectId { get; }

    /// <summary>Display name of the signed-in user, or <c>null</c> when anonymous.</summary>
    string? DisplayName { get; }
}
