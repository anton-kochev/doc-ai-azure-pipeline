namespace DocProcessing.Application.Configuration;

/// <summary>
/// Configuration options for OCR processing.
/// </summary>
public sealed class OcrOptions
{
    /// <summary>
    /// OCR provider to use (e.g., "Mock", "AzureDocumentIntelligence").
    /// </summary>
    public required string Provider { get; init; } = "Mock";

    /// <summary>
    /// Blob storage container name for storing OCR results.
    /// </summary>
    public required string OutputBlobContainer { get; init; } = "ocr-results";

    /// <summary>
    /// Timeout for OCR processing in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Azure Document Intelligence endpoint URL (when using AzureDocumentIntelligence provider).
    /// </summary>
    public string? DocumentIntelligenceEndpoint { get; init; }

    /// <summary>
    /// Model ID to use for document analysis (e.g., "prebuilt-layout", "prebuilt-document").
    /// </summary>
    public required string ModelId { get; init; } = "prebuilt-layout";

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Initial delay in seconds before first retry attempt.
    /// </summary>
    public int RetryDelaySeconds { get; init; } = 2;
}
