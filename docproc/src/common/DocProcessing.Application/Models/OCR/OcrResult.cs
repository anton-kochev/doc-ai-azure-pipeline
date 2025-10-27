namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Top-level OCR result containing all extracted information from a document.
/// </summary>
public sealed class OcrResult
{
    /// <summary>
    /// Document ID this OCR result is associated with.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Job ID for this OCR processing operation.
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// Pages extracted from the document.
    /// </summary>
    public IReadOnlyList<OcrPage> Pages { get; init; } = [];

    /// <summary>
    /// Metadata about the OCR processing operation.
    /// </summary>
    public OcrMetadata Metadata { get; init; }

    /// <summary>
    /// Blob storage path where the full OCR results are stored.
    /// </summary>
    public string? BlobPath { get; init; }

    public OcrResult(
        Guid documentId,
        Guid jobId,
        OcrMetadata metadata,
        IReadOnlyList<OcrPage>? pages = null,
        string? blobPath = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("Cannot be empty", nameof(documentId));
        if (jobId == Guid.Empty)
            throw new ArgumentException("Cannot be empty", nameof(jobId));

        DocumentId = documentId;
        JobId = jobId;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Pages = pages ?? [];
        BlobPath = blobPath;
    }
}
