using api.Data;
using api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Services;

/// <summary>
/// Implementation of document management operations.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(AppDbContext dbContext, ILogger<DocumentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> CreateDocumentAsync(
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
            UploadedAtUtc = DateTime.UtcNow,
            Status = DocumentStatus.Uploaded
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Created document record: DocumentId={DocumentId}, FileName={FileName}, Size={Size}",
            document.DocumentId,
            document.FileName,
            document.SizeBytes);

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
            _logger.LogInformation(
                "Found existing document with same hash: DocumentId={DocumentId}, FileName={FileName}",
                existingDocument.DocumentId,
                existingDocument.FileName);

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
}
