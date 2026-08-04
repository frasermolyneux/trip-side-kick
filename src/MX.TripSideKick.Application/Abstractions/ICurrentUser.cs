namespace MX.TripSideKick.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request.
/// </summary>
/// <remarks>
/// Backed by <c>MX.TripSideKick.Web.Hosting.HttpContextCurrentUser</c>, which reads claims from the
/// cookie-authenticated principal established by Microsoft Entra External ID (B2B collaboration and
/// self-service sign-up) on the app surface. <see cref="SubjectId"/> is the stable object id trip
/// membership is keyed on; <see cref="DisplayName"/> is PII and must never be logged or traced.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable subject identifier of the signed-in user, or <c>null</c> when anonymous.</summary>
    string? SubjectId { get; }

    /// <summary>Display name of the signed-in user, or <c>null</c> when anonymous.</summary>
    string? DisplayName { get; }
}
