using System.Text.Json;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(fileStream);

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

    /// <inheritdoc />
    public async Task<Stream> DownloadBlobAsync(string containerName, string blobPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        ValidateConfiguration();

        BlobServiceClient blobServiceClient = CreateBlobServiceClient();
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobPath);

        LogDownloadingBlob(containerName, blobPath);

        Azure.Response<Azure.Storage.Blobs.Models.BlobDownloadResult> response =
            await blobClient.DownloadContentAsync(cancellationToken);

        MemoryStream memoryStream = new();
        await response.Value.Content.ToStream().CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        LogBlobDownloaded(containerName, blobPath, memoryStream.Length);

        return memoryStream;
    }

    /// <inheritdoc />
    public async Task<string> UploadJsonAsync<T>(string containerName, string blobPath, T data, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);
        ArgumentNullException.ThrowIfNull(data);

        ValidateConfiguration();

        BlobServiceClient blobServiceClient = CreateBlobServiceClient();
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure the container exists
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        BlobClient blobClient = containerClient.GetBlobClient(blobPath);

        // Serialize to JSON
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        LogUploadingJson(containerName, blobPath, jsonBytes.Length);

        // Upload with JSON content type
        await using var stream = new MemoryStream(jsonBytes);
        await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = "application/json"
            }
        }, cancellationToken);

        LogJsonUploaded(containerName, blobPath);

        return $"{containerName}/{blobPath}";
    }

    /// <inheritdoc />
    public async Task<T?> DownloadJsonAsync<T>(string containerName, string blobPath, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        ValidateConfiguration();

        BlobServiceClient blobServiceClient = CreateBlobServiceClient();
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobPath);

        // Check if blob exists
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            LogBlobNotFound(containerName, blobPath);
            return null;
        }

        LogDownloadingJson(containerName, blobPath);

        Azure.Response<Azure.Storage.Blobs.Models.BlobDownloadResult> response =
            await blobClient.DownloadContentAsync(cancellationToken);

        var result = JsonSerializer.Deserialize<T>(response.Value.Content.ToMemory().Span);

        LogJsonDownloaded(containerName, blobPath);

        return result;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrEmpty(_options.ConnectionString) && string.IsNullOrEmpty(_options.AccountName))
        {
            throw new InvalidOperationException("Either Azure Storage connection string or account name must be configured");
        }
    }

    private BlobServiceClient CreateBlobServiceClient()
    {
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

        if (!string.IsNullOrEmpty(_options.ConnectionString))
        {
            return new BlobServiceClient(_options.ConnectionString, clientOptions);
        }
        else
        {
            Uri blobServiceUri = new($"https://{_options.AccountName}.blob.core.windows.net");
            return new BlobServiceClient(blobServiceUri, new DefaultAzureCredential(), clientOptions);
        }
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Configured Blob Storage retry policy: MaxRetries={MaxRetries}, Delay={Delay}s, MaxDelay={MaxDelay}s")]
    private partial void LogBlobStorageRetryPolicy(int maxRetries, int delay, int maxDelay);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Downloading blob from container '{ContainerName}', path '{BlobPath}'")]
    private partial void LogDownloadingBlob(string containerName, string blobPath);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Blob downloaded from container '{ContainerName}', path '{BlobPath}', size {SizeBytes} bytes")]
    private partial void LogBlobDownloaded(string containerName, string blobPath, long sizeBytes);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Uploading JSON to container '{ContainerName}', path '{BlobPath}', size {SizeBytes} bytes")]
    private partial void LogUploadingJson(string containerName, string blobPath, int sizeBytes);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "JSON uploaded to container '{ContainerName}', path '{BlobPath}'")]
    private partial void LogJsonUploaded(string containerName, string blobPath);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Downloading JSON from container '{ContainerName}', path '{BlobPath}'")]
    private partial void LogDownloadingJson(string containerName, string blobPath);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "JSON downloaded from container '{ContainerName}', path '{BlobPath}'")]
    private partial void LogJsonDownloaded(string containerName, string blobPath);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Blob not found in container '{ContainerName}', path '{BlobPath}'")]
    private partial void LogBlobNotFound(string containerName, string blobPath);
}
