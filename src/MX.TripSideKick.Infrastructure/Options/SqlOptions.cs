namespace MX.TripSideKick.Infrastructure.Options;

/// <summary>
/// Azure SQL connection settings.
/// </summary>
/// <remarks>
/// The connection string is always an Entra-only, password-free connection string
/// (<c>Authentication=Active Directory Default</c>) resolved through the App Service system-assigned
/// managed identity. No SQL logins or secrets are ever configured.
/// </remarks>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    public string? ConnectionString { get; set; }
}
