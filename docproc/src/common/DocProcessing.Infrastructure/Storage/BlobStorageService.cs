using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using DocProcessing.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Infrastructure.Storage;

/// <summary>
/// Implementation of Azure Blob Storage operations.
/// </summary>
public sealed partial class BlobStorageService : IStorageService
{
    private readonly AzureStorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        IOptions<AzureStorageOptions> options,
        ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when Azure Storage configuration is invalid (missing connection string and account name).
    /// </exception>
    /// <exception cref="Azure.RequestFailedException">
    /// Thrown when the upload operation fails due to Azure Storage errors.
    /// </exception>
    public async Task<UploadResult> UploadAsync(string fileName, Stream fileStream, string? contentType = null, CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        // Configure retry options for resilience against transient failures
        BlobClientOptions clientOptions = new()
        {
            Retry =
            {
                Mode = RetryMode.Exponential,
                MaxRetries = _options.MaxRetries,
                Delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds),
                MaxDelay = TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds),
                NetworkTimeout = TimeSpan.FromSeconds(100)
            }
        };

        LogBlobStorageRetryPolicy(_options.MaxRetries, _options.RetryDelaySeconds, _options.MaxRetryDelaySeconds);

        // Create BlobServiceClient - use connection string if provided, otherwise use Managed Identity
        BlobServiceClient blobServiceClient;
        if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            // Local development with Azurite or connection string
            blobServiceClient = new BlobServiceClient(_options.ConnectionString, clientOptions);
        }
        else
        {
            // Production with Managed Identity
            Uri blobServiceUri = new($"https://{_options.AccountName}.blob.core.windows.net");
            blobServiceClient = new BlobServiceClient(blobServiceUri, new DefaultAzureCredential(), clientOptions);
        }

        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_options.ContainerName);

        // Ensure the container exists (especially for Azurite)
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

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
            }, cancellationToken);

        // Get the blob properties to retrieve the size
        Azure.Storage.Blobs.Models.BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

        UploadResult result = new(
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

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Configured Blob Storage retry policy: MaxRetries={MaxRetries}, Delay={Delay}s, MaxDelay={MaxDelay}s")]
    private partial void LogBlobStorageRetryPolicy(int maxRetries, int delay, int maxDelay);
}
