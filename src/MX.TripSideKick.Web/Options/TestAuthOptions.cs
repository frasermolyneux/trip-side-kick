namespace MX.TripSideKick.Web.Options;

/// <summary>
/// Options for the Playwright/E2E-only deterministic sign-in endpoint. Security-sensitive - see
/// <c>MX.TripSideKick.Web.Hosting.TestAuthEndpoints</c> for the fail-closed gating this backs.
/// </summary>
public sealed class TestAuthOptions
{
    public const string SectionName = "TestAuth";

    /// <summary>
    /// Defaults to <c>false</c>. When enabled, the endpoint is mapped only in
    /// Development and when the App Service instance setting is absent. This is
    /// not a general detector for deployed hosts; never enable it on a public host.
    /// </summary>
    public bool Enabled { get; set; }
}
