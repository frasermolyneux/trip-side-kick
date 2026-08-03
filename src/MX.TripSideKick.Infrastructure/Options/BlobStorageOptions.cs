namespace MX.TripSideKick.Infrastructure.Options;

/// <summary>
/// Private Azure Blob Storage settings. Access is always via managed identity — the storage
/// account has public blob access disabled and shared keys are never used.
/// </summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>Blob service endpoint, e.g. <c>https://stortsk....blob.core.windows.net/</c>.</summary>
    public string? ServiceUri { get; set; }

    /// <summary>Container holding user-uploaded trip documents.</summary>
    public string DocumentsContainerName { get; set; } = "documents";

    /// <summary>Container holding persisted ASP.NET Core Data Protection keys.</summary>
    public string DataProtectionContainerName { get; set; } = "dataprotection";
}
