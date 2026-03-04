namespace DocProcessing.Application.Pipeline;

/// <summary>
/// Well-known metadata keys passed between pipeline stages via <see cref="StageContext"/>.
/// </summary>
public static class StageMetadataKeys
{
    public const string JobId = "jobId";
    public const string DocumentId = "documentId";
    public const string BlobContainer = "blobContainer";
    public const string BlobPath = "blobPath";
    public const string TenantId = "tenantId";
    public const string ExtractionProfile = "extractionProfile";
    public const string OcrBlobPath = "ocrBlobPath";
    public const string PreprocessBlobPath = "preprocessBlobPath";
    public const string ChunkBlobPath = "chunkBlobPath";
    public const string TotalChunks = "totalChunks";
    public const string TextChunks = "textChunks";
    public const string TableChunks = "tableChunks";
    public const string TotalTokens = "totalTokens";
    public const string ProcessingDurationMs = "processingDurationMs";
}
