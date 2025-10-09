namespace Api.Services;

/// <summary>
/// Service for managing Document entities.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Creates a new document record in the database.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <param name="sizeBytes">The size of the file in bytes.</param>
    /// <param name="blobContainer">The blob storage container name.</param>
    /// <param name="blobPath">The blob path/name in the container.</param>
    /// <param name="blobETag">The ETag from blob storage.</param>
    /// <param name="sha256Hash">The SHA256 hash of the file.</param>
    /// <param name="uploadedBy">The identifier of the user who uploaded the file.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <returns>The created document entity.</returns>
    Task<Guid> CreateDocumentAsync(
        string fileName,
        string contentType,
        long sizeBytes,
        string blobContainer,
        string blobPath,
        string blobETag,
        byte[] sha256Hash,
        string uploadedBy,
        Guid? tenantId = null);
}
