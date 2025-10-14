namespace DocProcessing.Api.Services;

/// <summary>
/// Service for managing Azure Blob Storage operations.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a blob directly to Azure Blob Storage using Managed Identity.
    /// </summary>
    /// <param name="fileName">The name of the file to upload.</param>
    /// <param name="fileStream">The file stream to upload.</param>
    /// <param name="contentType">Optional content type of the file.</param>
    /// <returns>Result containing the blob URL and metadata.</returns>
    Task<BlobUploadResult> UploadBlobAsync(string fileName, Stream fileStream, string? contentType = null);
}

/// <summary>
/// Result of blob upload.
/// </summary>
public record BlobUploadResult(
    string BlobUrl,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    string ETag,
    string ContainerName,
    string BlobPath
);
