namespace MX.TripSideKick.Infrastructure.Options;

/// <summary>
/// Azure SQL connection settings.
/// </summary>
/// <remarks>
/// The connection string is always an Entra-only, password-free connection string
/// (<c>Authentication=Active Directory Managed Identity;User Id=&lt;client-id&gt;</c>) resolved through
/// the App Service's dedicated data-access user-assigned managed identity. No SQL logins or secrets
/// are ever configured.
/// </remarks>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    public string? ConnectionString { get; set; }
}
