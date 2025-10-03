using Api.Configuration;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace Api.Services;

/// <summary>
/// Implementation of Azure Blob Storage operations.
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly AzureStorageOptions _options;

    public BlobStorageService(IOptions<AzureStorageOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<SasUrlResult> GenerateSasUrlAsync(string fileName, string? contentType = null)
    {
        ValidateConfiguration();

        BlobServiceClient blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        BlobContainerClient? containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        BlobClient? blobClient = containerClient.GetBlobClient(fileName);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Blob client cannot generate SAS URI. Ensure the connection string includes account key.");
        }

        BlobSasBuilder sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.ContainerName,
            BlobName = fileName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(_options.SasExpirationHours)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Create | BlobSasPermissions.Write);

        Uri? sasUri = blobClient.GenerateSasUri(sasBuilder);

        SasUrlResult result = new SasUrlResult(
            sasUri.ToString(),
            sasBuilder.ExpiresOn,
            fileName,
            contentType
        );

        return Task.FromResult(result);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_options.ConnectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string is not configured");
        }
    }
}
