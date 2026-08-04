namespace MX.TripSideKick.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request.
/// </summary>
/// <remarks>
/// Backed by <c>MX.TripSideKick.Web.Hosting.HttpContextCurrentUser</c>, which reads claims from the
/// cookie-authenticated principal established by Microsoft Entra External ID (B2B collaboration and
/// self-service sign-up) on the app surface. <see cref="SubjectId"/> is the stable object id trip
/// membership is keyed on; <see cref="DisplayName"/> and <see cref="VerifiedEmail"/> are PII and
/// must never be logged or traced.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable subject identifier of the signed-in user, or <c>null</c> when anonymous.</summary>
    string? SubjectId { get; }

    /// <summary>Display name of the signed-in user, or <c>null</c> when anonymous.</summary>
    string? DisplayName { get; }

    /// <summary>
    /// The verified email address Entra asserts for the signed-in user, or <c>null</c> when
    /// anonymous or the provider did not assert one. Used <em>only</em> to check whether an
    /// invitation may be accepted (docs/identity-and-access.md) - authorization itself is always
    /// keyed on <see cref="SubjectId"/>, never this value.
    /// </summary>
    string? VerifiedEmail { get; }
}

