using System.Security.Cryptography;
using DocProcessing.Application.Interfaces;

namespace DocProcessing.EndToEnd.Tests.Builders;

/// <summary>
/// Fluent builder that executes the same upload sequence as UploadFunctions.UploadFile:
/// 1. Compute SHA256 hash
/// 2. IStorageService.UploadAsync()
/// 3. IDocumentService.GetOrCreateDocumentAsync()
/// 4. IProcessJobService.GetOrCreateJobAsync()
/// 5. If new job: IMessagingService.EnqueueJobAsync()
/// </summary>
public sealed class UploadRequestBuilder
{
    private readonly IStorageService _storageService;
    private readonly IDocumentService _documentService;
    private readonly IProcessJobService _processJobService;
    private readonly IMessagingService _messagingService;

    private string _fileName = "test-document.pdf";
    private string _contentType = "application/pdf";
    private byte[] _fileContent = "fake PDF content"u8.ToArray();
    private string? _extractionProfile;
    private Guid? _tenantId;
    private string _uploadedBy = "test-user";

    public UploadRequestBuilder(
        IStorageService storageService,
        IDocumentService documentService,
        IProcessJobService processJobService,
        IMessagingService messagingService)
    {
        _storageService = storageService;
        _documentService = documentService;
        _processJobService = processJobService;
        _messagingService = messagingService;
    }

    public UploadRequestBuilder WithFileName(string fileName) { _fileName = fileName; return this; }
    public UploadRequestBuilder WithContentType(string contentType) { _contentType = contentType; return this; }
    public UploadRequestBuilder WithFileContent(byte[] content) { _fileContent = content; return this; }
    public UploadRequestBuilder WithExtractionProfile(string? profile) { _extractionProfile = profile; return this; }
    public UploadRequestBuilder WithTenantId(Guid? tenantId) { _tenantId = tenantId; return this; }
    public UploadRequestBuilder WithUploadedBy(string uploadedBy) { _uploadedBy = uploadedBy; return this; }

    public async Task<UploadResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // 1. Compute SHA256 hash
        byte[] sha256Hash = SHA256.HashData(_fileContent);

        // 2. Upload to storage
        using MemoryStream fileStream = new(_fileContent);
        var uploadResult = await _storageService.UploadAsync(_fileName, fileStream, _contentType, cancellationToken);

        // 3. Get or create document
        var (documentId, isNewDocument) = await _documentService.GetOrCreateDocumentAsync(
            _fileName, _contentType, uploadResult.FileSizeBytes,
            uploadResult.ContainerName, uploadResult.BlobPath, uploadResult.ETag,
            sha256Hash, _uploadedBy, _tenantId);

        // 4. Get or create job
        string correlationId = Guid.NewGuid().ToString();
        var (jobId, isNewJob) = await _processJobService.GetOrCreateJobAsync(
            documentId, _tenantId, sha256Hash,
            _extractionProfile, correlationId, 0, cancellationToken);

        // 5. If new job, enqueue
        if (isNewJob)
        {
            await _messagingService.EnqueueJobAsync(jobId, correlationId, cancellationToken);
        }

        return new UploadResult(documentId, jobId, isNewDocument, isNewJob, correlationId);
    }

    public record UploadResult(
        Guid DocumentId,
        Guid JobId,
        bool IsNewDocument,
        bool IsNewJob,
        string CorrelationId);
}
