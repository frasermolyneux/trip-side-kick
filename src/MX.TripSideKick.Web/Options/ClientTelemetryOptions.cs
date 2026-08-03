namespace MX.TripSideKick.Web.Options;

/// <summary>
/// Browser-side Application Insights configuration.
/// </summary>
public sealed class ClientTelemetryOptions
{
    public const string SectionName = "ApplicationInsights";

    /// <summary>Connection string handed to the Application Insights browser SDK.</summary>
    public string? ClientConnectionString { get; set; }
}
