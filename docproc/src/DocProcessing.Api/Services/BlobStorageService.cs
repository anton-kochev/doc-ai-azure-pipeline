using Azure.Identity;
using Azure.Storage.Blobs;
using DocProcessing.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DocProcessing.Api.Services;

/// <summary>
/// Implementation of Azure Blob Storage operations.
/// </summary>
public sealed class BlobStorageService : IBlobStorageService
{
    private readonly AzureStorageOptions _options;

    public BlobStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<BlobUploadResult> UploadBlobAsync(string fileName, Stream fileStream, string? contentType = null)
    {
        ValidateConfiguration();

        // Create BlobServiceClient - use connection string if provided, otherwise use Managed Identity
        BlobServiceClient blobServiceClient;
        if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            // Local development with Azurite or connection string
            blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        }
        else
        {
            // Production with Managed Identity
            Uri blobServiceUri = new($"https://{_options.AccountName}.blob.core.windows.net");
            blobServiceClient = new(blobServiceUri, new DefaultAzureCredential());
        }

        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);

        // Ensure the container exists (especially for Azurite)
        await containerClient.CreateIfNotExistsAsync();

        BlobClient blobClient = containerClient.GetBlobClient(fileName);

        // Set content type if provided
        Azure.Storage.Blobs.Models.BlobHttpHeaders? headers = contentType != null
            ? new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }
            : null;

        // Upload the blob
        Azure.Response<Azure.Storage.Blobs.Models.BlobContentInfo> uploadResponse =
            await blobClient.UploadAsync(fileStream, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = headers
            });

        // Get the blob properties to retrieve the size
        Azure.Storage.Blobs.Models.BlobProperties properties = await blobClient.GetPropertiesAsync();

        BlobUploadResult result = new(
            blobClient.Uri.ToString(),
            fileName,
            contentType,
            properties.ContentLength,
            uploadResponse.Value.ETag.ToString(),
            _options.ContainerName,
            fileName);

        return result;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_options.ConnectionString) && string.IsNullOrEmpty(_options.AccountName))
        {
            throw new InvalidOperationException("Either Azure Storage connection string or account name must be configured");
        }
    }
}
