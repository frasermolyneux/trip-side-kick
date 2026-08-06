namespace MX.TripSideKick.Web.Options;

/// <summary>
/// Options for the Playwright/E2E-only deterministic sign-in endpoint. Security-sensitive - see
/// <c>MX.TripSideKick.Web.Hosting.TestAuthEndpoints</c> for the fail-closed gating this backs.
/// </summary>
public sealed class TestAuthOptions
{
    public const string SectionName = "TestAuth";

    /// <summary>
    /// Must be explicitly set to <c>true</c> - defaults to <c>false</c> and is never set by any
    /// deployed environment's Terraform/app settings. Even when <c>true</c>, the endpoint is only
    /// mapped when the ASP.NET Core environment is <c>Development</c> <em>and</em> the process is
    /// not running as an Azure App Service instance (see
    /// <c>TestAuthEndpoints.MapTestAuthEndpoints</c> for why the environment check alone is not
    /// sufficient) - all conditions are required, so a single misconfiguration cannot expose it.
    /// </summary>
    public bool Enabled { get; set; }
}
