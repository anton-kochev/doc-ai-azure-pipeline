namespace DocProcessing.Application.Models.OCR;

/// <summary>
/// Represents a block of extracted text with position and confidence information.
/// </summary>
public sealed class TextBlock
{
    /// <summary>
    /// Extracted text content.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Confidence score for the extracted text (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Bounding box coordinates for this text block.
    /// </summary>
    public BoundingBox? BoundingBox { get; init; }

    /// <summary>
    /// Block type (e.g., "paragraph", "line", "word", "title", "header").
    /// </summary>
    public string BlockType { get; init; } = "paragraph";

    /// <summary>
    /// Language code detected (e.g., "en", "es", "fr").
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    /// Page number where this text block appears (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    public TextBlock(
        string text,
        double confidence,
        int pageNumber,
        BoundingBox? boundingBox = null,
        string blockType = "paragraph",
        string? languageCode = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Cannot be null or whitespace", nameof(text));
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Must be between 0 and 1");
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Must be >= 1");

        Text = text;
        Confidence = confidence;
        PageNumber = pageNumber;
        BoundingBox = boundingBox;
        BlockType = blockType;
        LanguageCode = languageCode;
    }
}
