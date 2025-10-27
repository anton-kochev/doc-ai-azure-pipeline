namespace DocProcessing.Application.Interfaces;

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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ID of the created document.</returns>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
    Task<Guid> CreateDocumentAsync(
        string fileName,
        string contentType,
        long sizeBytes,
        string blobContainer,
        string blobPath,
        string blobETag,
        byte[] sha256Hash,
        string uploadedBy,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing document by hash or creates a new one if it doesn't exist.
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
    /// <returns>A tuple containing the document ID and a boolean indicating whether it was newly created (true) or already existed (false).</returns>
    Task<(Guid DocumentId, bool IsNew)> GetOrCreateDocumentAsync(
        string fileName,
        string contentType,
        long sizeBytes,
        string blobContainer,
        string blobPath,
        string blobETag,
        byte[] sha256Hash,
        string uploadedBy,
        Guid? tenantId = null);

    /// <summary>
    /// Gets a document by its ID.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document, or null if not found.</returns>
    Task<Domain.Entities.Document?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the metadata JSON field of a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="metadataJson">The JSON metadata to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateDocumentMetadataAsync(Guid documentId, string metadataJson, CancellationToken cancellationToken = default);
}
