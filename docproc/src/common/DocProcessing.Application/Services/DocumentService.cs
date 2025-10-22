using DocProcessing.Application.Interfaces;
using DocProcessing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocProcessing.Application.Services;

/// <summary>
/// Implementation of document management operations.
/// </summary>
public sealed partial class DocumentService : IDocumentService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;
    private readonly TimeProvider _timeProvider;

    public DocumentService(
        IApplicationDbContext dbContext,
        ILogger<DocumentService> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    /// <exception cref="DbUpdateException">
    /// Thrown when the database update operation fails.
    /// </exception>
    public async Task<Guid> CreateDocumentAsync(
        string fileName,
        string contentType,
        long sizeBytes,
        string blobContainer,
        string blobPath,
        string blobETag,
        byte[] sha256Hash,
        string uploadedBy,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        Document document = new()
        {
            DocumentId = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            BlobContainer = blobContainer,
            BlobPath = blobPath,
            BlobETag = blobETag,
            Sha256Hash = sha256Hash,
            UploadedBy = uploadedBy,
            UploadedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            Status = DocumentStatus.Uploaded
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        LogDocumentCreated(document.DocumentId, document.FileName, document.SizeBytes);

        return document.DocumentId;
    }

    /// <inheritdoc />
    public async Task<(Guid DocumentId, bool IsNew)> GetOrCreateDocumentAsync(
        string fileName,
        string contentType,
        long sizeBytes,
        string blobContainer,
        string blobPath,
        string blobETag,
        byte[] sha256Hash,
        string uploadedBy,
        Guid? tenantId = null)
    {
        // Check if a document with the same hash already exists (excluding deleted documents)
        Document? existingDocument = await _dbContext.Documents
            .FirstOrDefaultAsync(d =>
                d.TenantId == tenantId &&
                d.Sha256Hash == sha256Hash &&
                d.Status != DocumentStatus.Deleted);

        if (existingDocument != null)
        {
            LogFoundExistingDocument(existingDocument.DocumentId, existingDocument.FileName);

            return (existingDocument.DocumentId, false);
        }

        // Document doesn't exist, create a new one
        Guid newDocumentId = await CreateDocumentAsync(
            fileName,
            contentType,
            sizeBytes,
            blobContainer,
            blobPath,
            blobETag,
            sha256Hash,
            uploadedBy,
            tenantId);

        return (newDocumentId, true);
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Created document record: DocumentId={DocumentId}, FileName={FileName}, Size={Size}")]
    private partial void LogDocumentCreated(Guid documentId, string fileName, long size);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Found existing document with same hash: DocumentId={DocumentId}, FileName={FileName}")]
    private partial void LogFoundExistingDocument(Guid documentId, string fileName);
}
