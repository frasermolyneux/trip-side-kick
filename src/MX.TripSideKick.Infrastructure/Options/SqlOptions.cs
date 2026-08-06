namespace MX.TripSideKick.Infrastructure.Options;

/// <summary>
/// Azure SQL connection settings.
/// </summary>
/// <remarks>
/// In the deployed App Service the connection string is an Entra-only, password-free connection
/// string (<c>Authentication=Active Directory Managed Identity;User Id=&lt;client-id&gt;</c>) resolved
/// through the dedicated data-access user-assigned managed identity - no SQL logins or secrets. Tests
/// and design-time tooling may instead supply a SQL-login / Testcontainers connection string.
/// </remarks>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    public string? ConnectionString { get; set; }
}
