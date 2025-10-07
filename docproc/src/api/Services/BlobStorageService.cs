using Api.Configuration;
using Azure.Identity;
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
    public async Task<SasUrlResult> GenerateSasUrlAsync(string fileName, string? contentType = null)
    {
        ValidateConfiguration();

        // Use Azure AD authentication instead of connection string
        Uri blobServiceUri = new($"https://{_options.AccountName}.blob.core.windows.net");
        BlobServiceClient blobServiceClient = new(blobServiceUri, new DefaultAzureCredential());
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        BlobClient blobClient = containerClient.GetBlobClient(fileName);

        // Get user delegation key for generating user delegation SAS
        DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(_options.SasExpirationHours);
        Azure.Storage.Blobs.Models.UserDelegationKey userDelegationKey =
            await blobServiceClient.GetUserDelegationKeyAsync(
                startsOn: DateTimeOffset.UtcNow.AddMinutes(-5),
                expiresOn: expiresOn);

        BlobSasBuilder sasBuilder = new()
        {
            BlobContainerName = _options.ContainerName,
            BlobName = fileName,
            Resource = "b",
            ExpiresOn = expiresOn
        };

        sasBuilder.SetPermissions(
            BlobSasPermissions.Read |
            BlobSasPermissions.Write |
            BlobSasPermissions.Create |
            BlobSasPermissions.Add
        );

        // Generate user delegation SAS
        BlobSasQueryParameters sasQueryParameters = sasBuilder.ToSasQueryParameters(
            userDelegationKey,
            _options.AccountName);

        UriBuilder sasUriBuilder = new(blobClient.Uri)
        {
            Query = sasQueryParameters.ToString()
        };

        SasUrlResult result = new(
            sasUriBuilder.Uri.ToString(),
            sasBuilder.ExpiresOn,
            fileName,
            contentType
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
