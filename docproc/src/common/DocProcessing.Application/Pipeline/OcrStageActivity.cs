using System.Text.Json;
using DocProcessing.Application.Configuration;
using DocProcessing.Application.Interfaces;
using DocProcessing.Application.Models.OCR;
using DocProcessing.Application.Services.OCR;
using DocProcessing.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocProcessing.Application.Pipeline;

/// <summary>
/// OCR stage activity that extracts text, tables, and form fields from documents.
/// </summary>
public sealed partial class OcrStageActivity : IJobStageActivity
{
    private readonly IOcrService _ocrService;
    private readonly IStorageService _storageService;
    private readonly IDocumentService _documentService;
    private readonly ILogger<OcrStageActivity> _logger;
    private readonly OcrOptions _options;

    public string StageName => "OCR";
    public ProcessJobStage Stage => ProcessJobStage.OCR;

    public OcrStageActivity(
        IOcrService ocrService,
        IStorageService storageService,
        IDocumentService documentService,
        ILogger<OcrStageActivity> logger,
        IOptions<OcrOptions> options)
    {
        _ocrService = ocrService;
        _storageService = storageService;
        _documentService = documentService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<StageResult> ExecuteAsync(StageContext context, CancellationToken cancellationToken = default)
    {
        Guid jobId = context.Job.JobId;
        Guid documentId = context.Job.DocumentId;

        LogOcrStageStarted(jobId, documentId);

        try
        {
            // 1. Get document from database
            Document? document = await _documentService.GetDocumentByIdAsync(documentId, cancellationToken);
            if (document is null)
            {
                LogDocumentNotFound(documentId);
                return StageResult.Failure(
                    "DOCUMENT_NOT_FOUND",
                    $"Document with ID {documentId} not found");
            }

            // 2. Download document from blob storage
            string? containerName = context.Metadata.TryGetValue("BlobContainer", out object? container)
                ? container.ToString()
                : document.BlobContainer;

            string? blobPath = context.Metadata.TryGetValue("BlobPath", out object? path)
                ? path.ToString()
                : document.BlobPath;

            LogDownloadingDocumentBlob(documentId, containerName ?? "unknown", blobPath ?? "unknown");

            await using Stream documentStream = await _storageService.DownloadBlobAsync(
                containerName!,
                blobPath!,
                cancellationToken);

            // 3. Perform OCR analysis
            OcrResult ocrResult = await _ocrService.AnalyzeDocumentAsync(
                documentId,
                jobId,
                documentStream,
                cancellationToken);

            // 4. Store full OCR results in blob storage
            string tenantId = context.Metadata.TryGetValue("TenantId", out object? tid)
                ? tid.ToString() ?? "default"
                : "default";

            string ocrBlobPath = $"{tenantId}/{documentId}/ocr-result.json";
            string ocrBlobFullPath = await _storageService.UploadJsonAsync(
                _options.OutputBlobContainer,
                ocrBlobPath,
                ocrResult,
                cancellationToken);

            LogOcrResultsStored(documentId, ocrBlobFullPath);

            // 5. Create summary for document metadata
            object metadataSummary = new
            {
                ocrCompleted = true,
                pageCount = ocrResult.Metadata.PageCount,
                confidence = ocrResult.Metadata.OverallConfidence,
                totalTextBlocks = ocrResult.Metadata.TotalTextBlocks,
                totalTables = ocrResult.Metadata.TotalTables,
                totalFormFields = ocrResult.Metadata.TotalFormFields,
                ocrBlobPath = ocrBlobFullPath,
                processedAt = ocrResult.Metadata.ProcessedAt,
                provider = ocrResult.Metadata.Provider
            };

            string metadataJson = JsonSerializer.Serialize(metadataSummary);

            // 6. Update document metadata
            await _documentService.UpdateDocumentMetadataAsync(documentId, metadataJson, cancellationToken);

            LogOcrStageCompleted(jobId, documentId, ocrResult.Metadata.PageCount, ocrResult.Metadata.OverallConfidence);

            // 7. Return success with output
            return StageResult.Success(
                output: new Dictionary<string, object>
                {
                    ["pageCount"] = ocrResult.Metadata.PageCount,
                    ["confidence"] = ocrResult.Metadata.OverallConfidence,
                    ["ocrBlobPath"] = ocrBlobFullPath
                });
        }
        catch (Exception ex)
        {
            LogOcrStageFailed(jobId, documentId, ex);

            return StageResult.Failure(
                "OCR_PROCESSING_FAILED",
                $"OCR processing failed: {ex.Message}");
        }
    }

    // Source-generated logging methods
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "OCR stage started for job {JobId}, document {DocumentId}")]
    private partial void LogOcrStageStarted(Guid jobId, Guid documentId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "Document {DocumentId} not found")]
    private partial void LogDocumentNotFound(Guid documentId);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Downloading document blob for document {DocumentId} from container '{ContainerName}', path '{BlobPath}'")]
    private partial void LogDownloadingDocumentBlob(Guid documentId, string containerName, string blobPath);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "OCR results for document {DocumentId} stored at '{OcrBlobPath}'")]
    private partial void LogOcrResultsStored(Guid documentId, string ocrBlobPath);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "OCR stage completed for job {JobId}, document {DocumentId}: {PageCount} pages, confidence {Confidence:F2}")]
    private partial void LogOcrStageCompleted(Guid jobId, Guid documentId, int pageCount, double confidence);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Error,
        Message = "OCR stage failed for job {JobId}, document {DocumentId}")]
    private partial void LogOcrStageFailed(Guid jobId, Guid documentId, Exception exception);
}
