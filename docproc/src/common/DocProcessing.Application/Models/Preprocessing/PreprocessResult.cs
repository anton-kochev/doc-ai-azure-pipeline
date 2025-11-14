namespace DocProcessing.Application.Models.Preprocessing;

/// <summary>
/// Represents the result of preprocessing a document.
/// Contains normalized text, structured tables, and parsed form fields.
/// </summary>
public sealed class PreprocessResult
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public Guid DocumentId { get; init; }

    /// <summary>
    /// Gets or sets the processing job identifier.
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// Gets or sets the normalized text content organized by page.
    /// </summary>
    public IReadOnlyList<PreprocessedPage> Pages { get; init; } = [];

    /// <summary>
    /// Gets or sets the structured tables extracted and normalized.
    /// </summary>
    public IReadOnlyList<StructuredTable> Tables { get; init; } = [];

    /// <summary>
    /// Gets or sets the normalized key-value pairs from form fields.
    /// </summary>
    public IReadOnlyList<NormalizedFormField> FormFields { get; init; } = [];

    /// <summary>
    /// Gets or sets metadata about the preprocessing operation.
    /// </summary>
    public required PreprocessMetadata Metadata { get; init; }

    /// <summary>
    /// Gets or sets the blob path where the full result is stored.
    /// </summary>
    public string? BlobPath { get; init; }
}
