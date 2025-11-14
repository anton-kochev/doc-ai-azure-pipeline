using DocProcessing.Application.Models.OCR;

namespace DocProcessing.Application.Models.Preprocessing;

/// <summary>
/// Represents a normalized text block from OCR output.
/// </summary>
public sealed class NormalizedTextBlock
{
    /// <summary>
    /// Gets or sets the original text from OCR.
    /// </summary>
    public string OriginalText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the normalized text (whitespace cleaned, Unicode normalized).
    /// </summary>
    public string NormalizedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the block type from OCR (paragraph, header, etc.).
    /// </summary>
    public string BlockType { get; init; } = "paragraph";

    /// <summary>
    /// Gets or sets the confidence score from OCR.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets or sets the bounding box from OCR.
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }

    /// <summary>
    /// Gets or sets the page number where this block appears.
    /// </summary>
    public int PageNumber { get; init; }
}
