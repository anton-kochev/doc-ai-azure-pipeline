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
