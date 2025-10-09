using api.Data;
using api.Data.Entities;
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
}
