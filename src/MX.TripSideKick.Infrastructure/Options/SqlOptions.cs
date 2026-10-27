using Npgsql;

namespace MX.TripSideKick.Infrastructure.Options;

/// <summary>PostgreSQL connection settings. Production credentials are read from a mounted secret.</summary>
public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    public string? ConnectionString { get; set; }

    public string? PasswordFile { get; set; }

    public string? GetConnectionString()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return null;
        }

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        if (!string.IsNullOrWhiteSpace(PasswordFile))
        {
            builder.Password = File.ReadAllText(PasswordFile).TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(builder.Password))
            {
                throw new InvalidOperationException("The database password file is empty.");
            }
        }

        builder.IncludeErrorDetail = false;
        // Password-authenticated private PostgreSQL must not attempt Windows domain discovery.
        builder.GssEncryptionMode = GssEncryptionMode.Disable;
        return builder.ConnectionString;
    }
}
