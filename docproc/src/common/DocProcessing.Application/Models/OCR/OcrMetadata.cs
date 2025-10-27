namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Metadata about the OCR processing operation.
/// </summary>
public sealed class OcrMetadata
{
    /// <summary>
    /// OCR provider/engine used (e.g., "Mock", "AzureDocumentIntelligence").
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Model or engine version used for OCR.
    /// </summary>
    public string? ModelVersion { get; init; }

    /// <summary>
    /// Total number of pages processed.
    /// </summary>
    public int PageCount { get; init; }

    /// <summary>
    /// Time when OCR processing started.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; init; }

    /// <summary>
    /// Duration of OCR processing.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }

    /// <summary>
    /// Overall confidence score across all pages (0.0 to 1.0).
    /// </summary>
    public double OverallConfidence { get; init; }

    /// <summary>
    /// Total number of text blocks extracted.
    /// </summary>
    public int TotalTextBlocks { get; init; }

    /// <summary>
    /// Total number of tables extracted.
    /// </summary>
    public int TotalTables { get; init; }

    /// <summary>
    /// Total number of form fields extracted.
    /// </summary>
    public int TotalFormFields { get; init; }

    /// <summary>
    /// Primary language detected in the document.
    /// </summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>
    /// Status of the OCR operation (e.g., "Success", "PartialSuccess", "Failed").
    /// </summary>
    public string Status { get; init; } = "Success";

    /// <summary>
    /// Any warnings or informational messages from OCR processing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public OcrMetadata(
        string provider,
        int pageCount,
        DateTimeOffset processedAt,
        TimeSpan processingDuration,
        double overallConfidence,
        int totalTextBlocks = 0,
        int totalTables = 0,
        int totalFormFields = 0,
        string? primaryLanguage = null,
        string? modelVersion = null,
        string status = "Success",
        IReadOnlyList<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Cannot be null or whitespace", nameof(provider));
        if (pageCount < 0)
            throw new ArgumentOutOfRangeException(nameof(pageCount), "Must be >= 0");
        if (overallConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(overallConfidence), "Must be between 0 and 1");

        Provider = provider;
        PageCount = pageCount;
        ProcessedAt = processedAt;
        ProcessingDuration = processingDuration;
        OverallConfidence = overallConfidence;
        TotalTextBlocks = totalTextBlocks;
        TotalTables = totalTables;
        TotalFormFields = totalFormFields;
        PrimaryLanguage = primaryLanguage;
        ModelVersion = modelVersion;
        Status = status;
        Warnings = warnings ?? [];
    }
}
