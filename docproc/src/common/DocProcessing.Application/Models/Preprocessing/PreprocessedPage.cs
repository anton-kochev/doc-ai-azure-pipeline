namespace DocProcessing.Application.Models.Preprocessing;

/// <summary>
/// Represents a preprocessed page with normalized text content.
/// </summary>
public sealed class PreprocessedPage
{
    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Gets or sets the full normalized text for this page (whitespace cleaned, Unicode normalized).
    /// </summary>
    public string NormalizedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the text blocks with normalization applied.
    /// </summary>
    public IReadOnlyList<NormalizedTextBlock> TextBlocks { get; init; } = [];

    /// <summary>
    /// Gets or sets the detected language (from OCR).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets or sets the word count after normalization.
    /// </summary>
    public int WordCount { get; init; }
}
