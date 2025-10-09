using Api.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace Api.Services;

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

        // Use Azure AD authentication (Managed Identity or DefaultAzureCredential)
        Uri blobServiceUri = new($"https://{_options.AccountName}.blob.core.windows.net");
        BlobServiceClient blobServiceClient = new(blobServiceUri, new DefaultAzureCredential());
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        BlobClient blobClient = containerClient.GetBlobClient(fileName);

        // Set content type if provided
        Azure.Storage.Blobs.Models.BlobHttpHeaders? headers = contentType != null
            ? new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }
            : null;

        // Upload the blob
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
            properties.ContentLength
        );

        return result;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_options.AccountName))
        {
            throw new InvalidOperationException("Azure Storage account name is not configured");
        }
    }
}
