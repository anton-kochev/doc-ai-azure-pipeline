namespace DocProcessing.Application.Interfaces;

/// <summary>
/// Service for managing Storage operations.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a blob directly to Azure Blob Storage using Managed Identity.
    /// </summary>
    /// <param name="fileName">The name of the file to upload.</param>
    /// <param name="fileStream">The file stream to upload.</param>
    /// <param name="contentType">Optional content type of the file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Result containing the blob URL and metadata.</returns>
    Task<UploadResult> UploadAsync(string fileName, Stream fileStream, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a blob from Azure Blob Storage.
    /// </summary>
    /// <param name="containerName">The container name.</param>
    /// <param name="blobPath">The blob path within the container.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Stream containing the blob content.</returns>
    Task<Stream> DownloadBlobAsync(string containerName, string blobPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a JSON object to Azure Blob Storage.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="containerName">The container name.</param>
    /// <param name="blobPath">The blob path within the container.</param>
    /// <param name="data">The data to serialize and upload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The full blob path.</returns>
    Task<string> UploadJsonAsync<T>(string containerName, string blobPath, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and deserializes a JSON blob from Azure Blob Storage.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="containerName">The container name.</param>
    /// <param name="blobPath">The blob path within the container.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The deserialized object, or null if not found.</returns>
    Task<T?> DownloadJsonAsync<T>(string containerName, string blobPath, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// Result of blob upload.
/// </summary>
public record UploadResult(
    string BlobUrl,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    string ETag,
    string ContainerName,
    string BlobPath
);
