using Azure.Identity;
using Azure.Storage.Blobs;

using Microsoft.Extensions.Options;

using MX.TripSideKick.Infrastructure.Options;

namespace MX.TripSideKick.Infrastructure.Storage;

/// <summary>
/// Factory for the private blob container client, authenticated with the hosting resource's
/// managed identity (locally: developer credentials or Azurite).
/// </summary>
public sealed class BlobStorageClientFactory(IOptions<BlobStorageOptions> options)
{
    private readonly BlobStorageOptions options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ServiceUri);

    public BlobContainerClient GetDocumentsContainer()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                $"'{BlobStorageOptions.SectionName}:{nameof(BlobStorageOptions.ServiceUri)}' is not configured.");
        }

        var serviceClient = new BlobServiceClient(new Uri(options.ServiceUri!), new DefaultAzureCredential());
        return serviceClient.GetBlobContainerClient(options.DocumentsContainerName);
    }
}
